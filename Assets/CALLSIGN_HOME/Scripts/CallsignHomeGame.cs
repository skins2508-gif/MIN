using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CallsignHome
{
    [Serializable]
    public sealed class RunState
    {
        public int chapter;
        public string lastCall = "없음";
        public bool helpedMechanic;
        public bool evacuatedWounded;
        public int commandTrust;
        public string finalChoice = "";
        public int battlesWon;

        public void Reset()
        {
            chapter = 0;
            lastCall = "없음";
            helpedMechanic = false;
            evacuatedWounded = false;
            commandTrust = 0;
            finalChoice = "";
            battlesWon = 0;
        }
    }

    public static class ChoiceResolver
    {
        public static void Apply(RunState state, int chapter, int option)
        {
            if (chapter == 0) state.lastCall = option == 0 ? "어머니" : "연인";
            else if (chapter == 1)
            {
                state.helpedMechanic = option == 0;
                state.commandTrust += option == 0 ? -1 : 1;
            }
            else if (chapter == 2)
            {
                state.evacuatedWounded = option == 0;
                state.commandTrust += option == 0 ? -1 : 1;
            }
            else if (chapter == 3) state.finalChoice = option == 0 ? "출진" : "항명";
        }
    }

    public sealed class CallsignHomeGame : MonoBehaviour
    {
        enum Mode { Title, Base, Flight, Refusal, Ending }

        sealed class Enemy
        {
            public Vector2 position;
            public float hp;
            public float speed;
            public bool boss;
            public float shotTimer;
        }

        sealed class Shot
        {
            public Vector2 position;
            public Vector2 velocity;
            public bool hostile;
            public bool missile;
        }

        sealed class Explosion
        {
            public Vector2 position;
            public float life = .72f;
        }

        static CallsignHomeGame instance;
        readonly RunState run = new RunState();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Explosion> explosions = new List<Explosion>();

        Mode mode = Mode.Title;
        Vector2 basePlayer = new Vector2(.5f, .56f);
        Vector2 flightPlayer = new Vector2(.5f, .82f);
        Texture2D baseRoom, corridor, infirmary, flightBackground, soldierSheet;
        Texture2D woundedSheet, playerPlane, enemyPlane, bossPlane, warning, phone;
        Texture2D wingPlane, missileSheet, bulletSheet, enemyExplosion, enemyDeathSheet, bombEffectSheet, darkPanel, buttonNormal, buttonHover;
        Material blackToAlphaMaterial;
        GUIStyle titleStyle, headingStyle, bodyStyle, subtitleStyle, smallStyle, buttonStyle, singleLineButtonStyle, panelStyle, topBarStyle, markerStyle;
        bool stylesReady;
        bool choiceOpen;
        bool choiceLocked;
        string message = "";
        float messageUntil;
        string feedback = "";
        float feedbackUntil;
        float flightTime;
        float fireTimer;
        float spawnTimer;
        float invulnerable;
        int hp;
        int kills;
        int targetKills;
        int missiles;
        float wingFireTimer;
        int refusalStep = 0;
        float choiceUndoUntil;
        int previousChoice = -1;
        bool showControls;
        Vector2 lastBaseInput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("CALLSIGN_HOME_Game");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CallsignHomeGame>();
        }

        void Awake()
        {
            baseRoom = Resources.Load<Texture2D>("Art/BaseRoom");
            corridor = Resources.Load<Texture2D>("Art/Corridor");
            infirmary = Resources.Load<Texture2D>("Art/Infirmary");
            flightBackground = Resources.Load<Texture2D>("Art/FlightBackground");
            soldierSheet = Resources.Load<Texture2D>("Art/SoldierSheet");
            woundedSheet = Resources.Load<Texture2D>("Art/WoundedSheet");
            playerPlane = Resources.Load<Texture2D>("Art/PlayerPlaneGray");
            enemyPlane = Resources.Load<Texture2D>("Art/EnemyPlane");
            bossPlane = Resources.Load<Texture2D>("Art/BossPlane");
            warning = Resources.Load<Texture2D>("Art/Warning");
            phone = Resources.Load<Texture2D>("Art/PhoneGame2D");
            wingPlane = Resources.Load<Texture2D>("Art/WingPlane");
            missileSheet = Resources.Load<Texture2D>("Art/MissileSheet");
            bulletSheet = Resources.Load<Texture2D>("Art/BulletSheet");
            enemyExplosion = Resources.Load<Texture2D>("Art/EnemyExplosion");
            enemyDeathSheet = Resources.Load<Texture2D>("Art/EnemyDeathSheet");
            bombEffectSheet = Resources.Load<Texture2D>("Art/BombEffectSheet");
            Shader blackToAlpha = Shader.Find("CALLSIGN/BlackToAlpha");
            if (blackToAlpha != null) blackToAlphaMaterial = new Material(blackToAlpha);
            run.Reset();
        }

        void Update()
        {
            if (Keyboard.current == null) return;
            if (mode == Mode.Title && Keyboard.current.yKey.wasPressedThisFrame) showControls = !showControls;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (choiceOpen) choiceOpen = false;
                else if (mode != Mode.Title) ShowFeedback("ESC: 선택 창 닫기 · WASD 이동 · E 조사 · SPACE 사격");
            }

            if (mode == Mode.Base || mode == Mode.Refusal) UpdateBase();
            else if (mode == Mode.Flight) UpdateFlight();
        }

        void UpdateBase()
        {
            if (choiceOpen)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame) ApplyChoice(0);
                else if (Keyboard.current.rKey.wasPressedThisFrame) ApplyChoice(1);
                return;
            }
            if (mode == Mode.Base && run.chapter < 3 && !choiceLocked && Keyboard.current.eKey.wasPressedThisFrame)
            {
                choiceOpen = true;
                return;
            }
            if (mode == Mode.Refusal && Keyboard.current.eKey.wasPressedThisFrame)
            {
                FinishEnding();
                return;
            }
            Vector2 input = ReadMovement();
            lastBaseInput = input;
            basePlayer += input.normalized * Time.unscaledDeltaTime * .27f;
            basePlayer.x = Mathf.Clamp(basePlayer.x, .31f, .69f);
            basePlayer.y = Mathf.Clamp(basePlayer.y, .055f, .89f);

            if (mode == Mode.Base && run.chapter == 3)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                    OpenFinalChoice(0);
                else if (Keyboard.current.rKey.wasPressedThisFrame)
                    OpenFinalChoice(1);
                return;
            }

            if (!InteractPressed()) return;
            if (mode == Mode.Base && run.chapter < 3 && choiceLocked)
            {
                StartFlight();
                return;
            }
            if (mode == Mode.Refusal)
            {
                FinishEnding();
                return;
            }

            if (run.chapter < 3)
            {
                return;
            }
            else ShowFeedback(CurrentInvestigation());
        }

        void OpenFinalChoice(int option)
        {
            ChoiceResolver.Apply(run, 3, option);
            if (option == 0) StartFlight(true);
            else
            {
                FinishEnding();
            }
        }

        void UpdateFlight()
        {
            float dt = Time.deltaTime;
            flightTime -= dt;
            fireTimer -= dt;
            spawnTimer -= dt;
            invulnerable -= dt;
            Vector2 input = ReadMovement();
            flightPlayer += input.normalized * dt * .52f;
            flightPlayer.x = Mathf.Clamp(flightPlayer.x, .08f, .92f);
            flightPlayer.y = Mathf.Clamp(flightPlayer.y, .12f, .90f);

            if (Keyboard.current.spaceKey.isPressed && fireTimer <= 0)
            {
                float rate = run.evacuatedWounded ? .22f : .16f;
                shots.Add(new Shot { position = flightPlayer + Vector2.down * .045f, velocity = Vector2.down * .9f });
                fireTimer = rate;
            }
            if (Keyboard.current.eKey.wasPressedThisFrame && missiles > 0) FireMissile();
            if (run.chapter == 0)
            {
                wingFireTimer -= dt;
                if (wingFireTimer <= 0)
                {
                    shots.Add(new Shot { position = flightPlayer + new Vector2(-.13f, .01f), velocity = Vector2.down * .78f });
                    wingFireTimer = .48f;
                }
            }

            bool finalBattle = run.chapter == 3 && run.finalChoice == "출진";
            if (spawnTimer <= 0 && (!finalBattle || enemies.Count == 0))
            {
                SpawnEnemy(finalBattle);
                spawnTimer = finalBattle ? 99f : Mathf.Lerp(1.8f, 1.1f, run.chapter / 2f);
            }

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                enemy.position.y += enemy.speed * dt;
                if (enemy.boss) enemy.position.x = .5f + Mathf.Sin(Time.time * 1.4f) * .28f;
                enemy.shotTimer -= dt;
                if (enemy.shotTimer <= 0)
                {
                    FireEnemy(enemy);
                    enemy.shotTimer = enemy.boss ? .75f : 1.7f;
                }
                if (enemy.position.y > 1.1f || enemy.position.y < -.15f) enemies.RemoveAt(i);
            }

            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot shot = shots[i];
                shot.position += shot.velocity * dt;
                bool removed = false;
                if (!shot.hostile)
                {
                    for (int e = enemies.Count - 1; e >= 0; e--)
                    {
                        if (Vector2.Distance(shot.position, enemies[e].position) < (enemies[e].boss ? .10f : .065f))
                        {
                            enemies[e].hp -= shot.missile ? 6f : 1f;
                            if (enemies[e].hp <= 0)
                            {
                                bool boss = enemies[e].boss;
                                explosions.Add(new Explosion { position = enemies[e].position });
                                enemies.RemoveAt(e);
                                kills++;
                                if (boss) flightTime = Mathf.Min(flightTime, 1.2f);
                            }
                            removed = true;
                            break;
                        }
                    }
                }
                else if (invulnerable <= 0 && Vector2.Distance(shot.position, flightPlayer) < .055f)
                {
                    hp--;
                    invulnerable = 1f;
                    removed = true;
                    ShowFeedback("피격! 1초 동안 재피격되지 않습니다.");
                    if (hp <= 0)
                    {
                        hp = run.helpedMechanic ? 4 : 3;
                        flightPlayer = new Vector2(.5f, .82f);
                        shots.Clear();
                        ShowFeedback("긴급 복귀: 현재 전투를 이어갑니다.");
                        break;
                    }
                }
                if (removed || shot.position.y < -.1f || shot.position.y > 1.1f) shots.RemoveAt(i);
            }

            for (int i = explosions.Count - 1; i >= 0; i--)
            {
                explosions[i].life -= dt;
                if (explosions[i].life <= 0) explosions.RemoveAt(i);
            }

            if (flightTime <= 0 || (!finalBattle && kills >= targetKills)) CompleteFlight();
        }

        void SpawnEnemy(bool boss)
        {
            enemies.Add(new Enemy
            {
                position = new Vector2(boss ? .5f : UnityEngine.Random.Range(.12f, .88f), -.08f),
                hp = boss ? 18 : 2,
                speed = boss ? .08f : UnityEngine.Random.Range(.10f, .18f),
                boss = boss,
                shotTimer = boss ? .5f : 1.1f
            });
        }

        void FireEnemy(Enemy enemy)
        {
            Vector2 dir = (flightPlayer - enemy.position).normalized;
            if (!enemy.boss) shots.Add(new Shot { position = enemy.position, velocity = dir * .34f, hostile = true });
            else
            {
                for (int i = -1; i <= 1; i++)
                {
                    float a = i * 18f * Mathf.Deg2Rad;
                    Vector2 spread = new Vector2(dir.x * Mathf.Cos(a) - dir.y * Mathf.Sin(a), dir.x * Mathf.Sin(a) + dir.y * Mathf.Cos(a));
                    shots.Add(new Shot { position = enemy.position, velocity = spread * .38f, hostile = true });
                }
            }
        }

        void FireMissile()
        {
            missiles--;
            shots.Add(new Shot
            {
                position = flightPlayer + Vector2.down * .07f,
                velocity = Vector2.down * .62f,
                missile = true
            });
        }

        void CompleteFlight()
        {
            run.battlesWon++;
            enemies.Clear();
            shots.Clear();
            explosions.Clear();
            if (run.chapter == 3) FinishEnding();
            else
            {
                run.chapter++;
                mode = Mode.Base;
                basePlayer = new Vector2(.5f, .56f);
                choiceLocked = false;
                choiceOpen = false;
                previousChoice = -1;
                SetMessage(ChapterIntro());
            }
        }

        void StartFlight(bool final = false)
        {
            mode = Mode.Flight;
            choiceOpen = false;
            enemies.Clear();
            shots.Clear();
            explosions.Clear();
            flightPlayer = new Vector2(.5f, .82f);
            hp = run.helpedMechanic ? 4 : 3;
            missiles = 3;
            wingFireTimer = .2f;
            kills = 0;
            targetKills = run.commandTrust > 0 ? 4 : 6;
            flightTime = final ? 45f : run.evacuatedWounded ? 32f : 25f;
            spawnTimer = .6f;
            if (final) SpawnEnemy(true);
        }

        void FinishEnding()
        {
            mode = Mode.Ending;
            enemies.Clear();
            shots.Clear();
            explosions.Clear();
        }

        Vector2 ReadMovement()
        {
            Vector2 v = Vector2.zero;
            var k = Keyboard.current;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y -= 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y += 1;
            return v;
        }

        bool InteractPressed()
        {
            return Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        Vector2 ChoicePoint()
        {
            if (run.chapter == 0) return new Vector2(.5f, .47f);
            if (run.chapter == 1) return new Vector2(.37f, .40f);
            return new Vector2(.63f, .40f);
        }

        static Vector2 FinalSortiePoint() { return new Vector2(.34f, .16f); }
        static Vector2 FinalRefusalPoint() { return new Vector2(.66f, .16f); }

        string CurrentInvestigation()
        {
            if (run.chapter == 0) return "전화 교환대는 연결 가능한 두 번호를 표시한다. 한 사람에게만 전화할 수 있다.";
            if (run.chapter == 1) return "정비 보고서와 명령서가 같은 책상 위에 놓여 있다. 둘 다 지킬 수는 없다.";
            if (run.chapter == 2) return "부상병 후송 차량과 탄약고 방어 명령이 동시에 도착했다.";
            return "왼쪽 출격문과 오른쪽 반납 통로. 마지막 명령의 대답은 행동으로 남는다.";
        }

        string ChapterIntro()
        {
            if (run.chapter == 1) return "제2야간 · 보급 부족\n정비병은 부품을 요청하고, 상관은 명령서부터 전달하라고 한다.";
            if (run.chapter == 2) return "제3야간 · 철수\n한 대뿐인 차량을 부상병에게 쓸지, 탄약을 나를지 결정해야 한다.";
            return "마지막 밤 · 특별 출격 명령\n국가는 에이스에게 새 기체를 내놓았다. 이제 출진하거나 거부하라.";
        }

        void ShowFeedback(string text)
        {
            feedback = text;
            feedbackUntil = Time.unscaledTime + 1.4f;
        }

        void SetMessage(string text, float seconds = 1.5f)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        void OnGUI()
        {
            EnsureStyles();
            if (mode == Mode.Title) DrawTitle();
            else if (mode == Mode.Base || mode == Mode.Refusal) DrawBase();
            else if (mode == Mode.Flight) DrawFlight();
            else DrawEnding();
            if (mode != Mode.Flight && Time.unscaledTime < feedbackUntil) DrawFeedback();
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            int unit = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 54f));
            darkPanel = MakeTexture(new Color(0f, 0f, 0f, 0f));
            buttonNormal = MakeTexture(new Color(.32f, .15f, .045f, .88f));
            buttonHover = MakeTexture(new Color(.48f, .25f, .07f, .94f));
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = unit * 3, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, .72f, .28f) } };
            headingStyle = new GUIStyle(GUI.skin.label) { fontSize = unit + 9, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = unit + 1, fontStyle = FontStyle.Bold, wordWrap = true, clipping = TextClipping.Overflow, normal = { textColor = Color.white } };
            subtitleStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = unit + 1, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.93f, .95f, .92f) } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = unit + 3, fontStyle = FontStyle.Bold, wordWrap = true, padding = new RectOffset(18, 18, 12, 12), normal = { textColor = Color.white, background = buttonNormal }, hover = { textColor = new Color(1f, .78f, .32f), background = buttonHover }, active = { textColor = Color.white, background = buttonHover } };
            singleLineButtonStyle = new GUIStyle(buttonStyle)
            {
                wordWrap = false,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 6, 6)
            };
            panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 16, 16), normal = { background = darkPanel } };
            topBarStyle = new GUIStyle(panelStyle) { padding = new RectOffset(18, 18, 8, 8) };
            markerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = unit + 2,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(8, 8, 4, 4),
                normal = { textColor = new Color(1f, .78f, .30f) }
            };
            stylesReady = true;
        }

        void DrawTitle()
        {
            DrawCover(baseRoom, new Color(.10f, .11f, .12f, .58f));
            float w = Mathf.Min(760, Screen.width * .8f);
            GUI.Label(new Rect((Screen.width - w) / 2, Screen.height * .035f, w, 82), "CALLSIGN: HOME", titleStyle);
            GUI.Label(new Rect((Screen.width - w) / 2, Screen.height * .22f, w, 78), "기지에서 사람과 명령 사이를 선택하고,\n하늘에서 그 대가를 견디는 탑다운 전쟁 게임", subtitleStyle);
            if (GUI.Button(new Rect(Screen.width * .35f, Screen.height * .46f, Screen.width * .3f, 64), "새 게임", buttonStyle))
            {
                run.Reset();
                mode = Mode.Base;
                basePlayer = new Vector2(.5f, .56f);
                choiceLocked = false;
                choiceOpen = false;
                SetMessage("제1야간 · 축하\n방송은 승리를 말하지만, 전화기 너머의 목소리는 다르다.");
            }
            GUI.Label(new Rect(Screen.width * .41f, Screen.height * .64f, Screen.width * .18f, 36), "Y  조작법", headingStyle);
            if (showControls)
            {
                Rect controls = new Rect(Screen.width * .25f, Screen.height * .70f, Screen.width * .50f, 112);
                GUI.Box(controls, "", panelStyle);
                GUI.Label(new Rect(controls.x + 18, controls.y + 12, controls.width - 36, controls.height - 24),
                    "기지: WASD/방향키 이동 · E 조사/결정\n전투: WASD 이동 · SPACE 총알 · E 미사일\nY: 조작법 닫기 · ESC: 도움말", bodyStyle);
            }
        }

        void DrawBase()
        {
            Texture2D bg = mode == Mode.Refusal ? corridor : run.chapter >= 3 ? baseRoom : infirmary;
            DrawCover(bg, run.chapter >= 2 ? new Color(.12f, .13f, .16f, .27f) : Color.clear);
            DrawTopBar(mode == Mode.Refusal ? "항명 · 장비 반납" : ChapterName(), mode == Mode.Refusal ? refusalStep + "/3" : choiceLocked ? "선택 1/1" : "선택 0/1");

            if (mode == Mode.Base && run.chapter == 3) DrawFinalDoorOverlays();

            Rect p = NormalizedRect(basePlayer, .065f, .105f);
            if (soldierSheet != null) GUI.DrawTextureWithTexCoords(p, soldierSheet, SoldierUv(), true);
            else GUI.Box(p, "P");

            if (mode == Mode.Base && run.chapter == 0) DrawPhone(ChoicePoint());

            if (mode == Mode.Refusal)
            {
                Vector2[] pts = { new Vector2(.38f, .72f), new Vector2(.62f, .48f), new Vector2(.50f, .18f) };
                if (refusalStep < 3) DrawMarker(pts[refusalStep], "E 반납");
            }
            else if (run.chapter < 3)
            {
                if (choiceLocked) DrawMarker(new Vector2(.5f, .34f), "E  출격");
                else if (!choiceOpen) DrawMarker(new Vector2(.5f, .24f), "E 조사");
            }
            else
            {
                DrawMarker(FinalSortiePoint(), "E  출진");
                DrawMarker(FinalRefusalPoint(), "R  항명");
            }

            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                Rect r = new Rect(Screen.width * .12f, Screen.height * .68f, Screen.width * .76f, Screen.height * .27f);
                GUI.Box(r, "", panelStyle);
                GUI.Label(new Rect(r.x + 24, r.y + 18, r.width - 48, r.height - 36), message, bodyStyle);
            }
            if (mode == Mode.Base && choiceLocked && Time.unscaledTime < choiceUndoUntil)
            {
                if (GUI.Button(new Rect(Screen.width - 320, 72, 300, 64), "선택 취소 (3초)", singleLineButtonStyle)) UndoChoice();
            }
            if (choiceOpen) DrawChoicePanel();
        }

        void DrawChoicePanel()
        {
            Rect r = new Rect(Screen.width * .21f, Screen.height * .20f, Screen.width * .58f, Screen.height * .55f);
            GUI.Box(r, "", panelStyle);
            GUI.Label(new Rect(r.x + 24, r.y + 20, r.width - 48, 70), ChoiceQuestion(), headingStyle);
            string[] options = ChoiceOptions();
            for (int i = 0; i < 2; i++)
            {
                string key = i == 0 ? "E" : "R";
                if (GUI.Button(new Rect(r.x + 30, r.y + 105 + i * 105, r.width - 60, 82), key + "  " + options[i], buttonStyle)) ApplyChoice(i);
            }
        }

        void ApplyChoice(int option)
        {
            previousChoice = option;
            ChoiceResolver.Apply(run, run.chapter, option);
            choiceLocked = true;
            choiceUndoUntil = Time.unscaledTime + 3f;
            choiceOpen = false;
            SetMessage(ChoiceResult(option) + "\n출격하세요.");
            ShowFeedback("선택이 기록되었습니다. 3초 동안 취소할 수 있습니다.");
        }

        void UndoChoice()
        {
            if (run.chapter == 0) run.lastCall = "없음";
            else if (run.chapter == 1)
            {
                run.helpedMechanic = false;
                run.commandTrust -= previousChoice == 0 ? -1 : 1;
            }
            else if (run.chapter == 2)
            {
                run.evacuatedWounded = false;
                run.commandTrust -= previousChoice == 0 ? -1 : 1;
            }
            choiceLocked = false;
            previousChoice = -1;
            SetMessage(ChapterIntro());
        }

        string ChoiceQuestion()
        {
            if (run.chapter == 0) return "마지막으로 누구의 목소리를 들을 것인가?";
            if (run.chapter == 1) return "사람을 고칠 것인가, 명령을 전달할 것인가?";
            return "누구에게 마지막 차량을 내어줄 것인가?";
        }

        string[] ChoiceOptions()
        {
            if (run.chapter == 0) return new[] { "어머니에게 전화한다\n고향의 현실을 듣는다", "연인에게 전화한다\n명령을 거부할 말을 듣는다" };
            if (run.chapter == 1) return new[] { "정비병을 돕는다\n기체 HP +1 · 상관 신뢰 하락", "명령서를 전달한다\n다음 출격의 적 감소 · 정비병 이탈" };
            return new[] { "부상병을 후송한다\n항명 경로 확보 · 전투 시간 증가", "탄약고를 방어한다\n연사 속도 증가 · 빈 침상 증가" };
        }

        string ChoiceResult(int option)
        {
            if (run.chapter == 0) return option == 0 ? "어머니: '우린 괜찮다.' 그 말 뒤로 배급 방송이 끊겼다." : "연인: '살아서 돌아오는 건 비겁함이 아니야.'";
            if (run.chapter == 1) return option == 0 ? "정비병은 기체에 마지막 장갑판을 붙였다. 상관은 아무 말도 하지 않았다." : "명령은 제때 도착했다. 정비병의 작업대는 비었다.";
            return option == 0 ? "후송차가 어둠 속으로 떠났다. 탄약 운반은 중단됐다." : "탄약고는 지켰다. 의무실의 침상 하나가 더 비었다.";
        }

        void DrawFlight()
        {
            DrawCover(flightBackground, new Color(.08f, .09f, .10f, .15f));
            DrawTopBar("출격 " + (run.chapter + 1), "HP " + new string('■', hp) + "   미사일 " + missiles + "/3   " + Mathf.CeilToInt(flightTime) + "초   격추 " + kills + "/" + targetKills);
            Rect playerRect = NormalizedRect(flightPlayer, .12f, .12f);
            if (playerPlane != null) GUI.DrawTexture(playerRect, playerPlane, ScaleMode.ScaleToFit, true);
            if (run.chapter == 0 && wingPlane != null)
            {
                GUI.DrawTexture(NormalizedRect(flightPlayer + new Vector2(-.13f, .055f), .085f, .085f), wingPlane, ScaleMode.ScaleToFit, true);
            }
            foreach (Enemy e in enemies)
            {
                Rect er = NormalizedRect(e.position, e.boss ? .18f : .11f, e.boss ? .18f : .11f);
                Texture2D tex = e.boss ? bossPlane : enemyPlane;
                if (tex != null) DrawRotatedTexture(er, tex, 180f);
                if (e.boss) GUI.Label(new Rect(er.x, er.y - 24, er.width, 22), "전설의 에이스", smallStyle);
            }
            foreach (Shot s in shots)
            {
                Rect sr = NormalizedRect(s.position, s.hostile ? .018f : s.missile ? .042f : .024f, s.hostile ? .035f : s.missile ? .085f : .055f);
                if (s.missile && missileSheet != null)
                    GUI.DrawTextureWithTexCoords(sr, missileSheet, new Rect(.17f, .82f, .075f, .17f), true);
                else if (bulletSheet != null)
                    GUI.DrawTextureWithTexCoords(sr, bulletSheet, new Rect(.02f, .66f, .07f, .28f), true);
                else GUI.DrawTexture(sr, Texture2D.whiteTexture);
            }
            foreach (Explosion ex in explosions)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(ex.life * 2.2f));
                if (enemyDeathSheet != null)
                {
                    int frame = Mathf.Clamp(Mathf.FloorToInt((1f - ex.life / .72f) * 12f), 0, 11);
                    int col = frame % 4;
                    int row = frame / 4;
                    Rect uv = new Rect(col * .25f, 1f - (row + 1) / 3f, .25f, 1f / 3f);
                    Rect er = NormalizedRect(ex.position, .20f, .20f);
                    if (blackToAlphaMaterial != null)
                        Graphics.DrawTexture(er, enemyDeathSheet, uv, 0, 0, 0, 0, Color.white, blackToAlphaMaterial);
                    else
                        GUI.DrawTextureWithTexCoords(er, enemyDeathSheet, uv, true);
                }
                GUI.color = old;
            }
        }

        void DrawEnding()
        {
            bool sortie = run.finalChoice == "출진";
            DrawCover(sortie ? flightBackground : corridor, new Color(.04f, .04f, .05f, .72f));
            float w = Mathf.Min(850, Screen.width * .82f);
            GUI.Label(new Rect((Screen.width - w) / 2, Screen.height * .12f, w, 90), sortie ? "결말 A · 전설의 포로" : "결말 B · 이름 없는 귀환", titleStyle);
            string ending = sortie
                ? "최신 기체로 적 에이스를 쓰러뜨렸지만, 축하 무전보다 수도 함락 뉴스가 먼저 도착했다.\n\n그가 지킨 하늘은 사라졌고, 적들은 그의 이름보다 격추 수를 오래 기억했다."
                : "국가는 패전의 책임을 출격을 거부한 에이스에게 돌렸다. 그는 살아서 돌아왔지만 공훈과 관계를 잃었다.\n\n그는 살아서 집으로 돌아왔지만, 돌아갈 집은 더 이상 남아 있지 않았다.";
            GUI.Box(new Rect((Screen.width - w) / 2, Screen.height * .29f, w, Screen.height * .36f), "", panelStyle);
            GUI.Label(new Rect((Screen.width - w) / 2 + 28, Screen.height * .32f, w - 56, Screen.height * .25f), ending, bodyStyle);
            GUI.Label(new Rect((Screen.width - w) / 2 + 28, Screen.height * .56f, w - 56, 40), "마지막 통화: " + run.lastCall + " · 전투 완료: " + run.battlesWon + " · 명령 신뢰: " + TrustLabel(), smallStyle);
            if (GUI.Button(new Rect(Screen.width * .35f, Screen.height * .73f, Screen.width * .30f, 58), "타이틀로 돌아가기", buttonStyle)) mode = Mode.Title;
        }

        string TrustLabel() { return run.commandTrust > 0 ? "높음" : run.commandTrust < 0 ? "낮음" : "불명"; }
        string ChapterName() { return run.chapter == 0 ? "제1야간 · 축하" : run.chapter == 1 ? "제2야간 · 보급 부족" : run.chapter == 2 ? "제3야간 · 철수" : "마지막 밤"; }

        void DrawTopBar(string left, string right)
        {
            GUI.Box(new Rect(0, 0, Screen.width, 64), "", topBarStyle);
            GUI.Label(new Rect(20, 10, Screen.width * .65f, 40), left, headingStyle);
            GUI.Label(new Rect(Screen.width * .65f, 13, Screen.width * .32f, 36), right, bodyStyle);
        }

        void DrawMarker(Vector2 pos, string label)
        {
            Rect anchor = NormalizedRect(pos, .01f, .01f);
            Rect r = new Rect(anchor.center.x - 120, anchor.center.y + 20, 240, 64);
            GUI.DrawTexture(r, buttonNormal);
            GUI.Label(new Rect(r.x + 6, r.y + 2, r.width - 12, r.height - 4), label, markerStyle);
        }

        void DrawFinalDoorOverlays()
        {
            Color old = GUI.color;
            GUI.color = new Color(.25f, .24f, .21f, .98f);
            Vector2[] signs =
            {
                new Vector2(.30f, .19f), new Vector2(.70f, .19f),
                new Vector2(.30f, .45f), new Vector2(.70f, .45f),
                new Vector2(.30f, .71f), new Vector2(.70f, .71f)
            };
            foreach (Vector2 sign in signs)
            {
                Rect r = NormalizedRect(sign, .13f, .045f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        void DrawPhone(Vector2 pos)
        {
            Rect r = NormalizedRect(pos, .095f, .17f);
            if (phone != null) GUI.DrawTexture(r, phone, ScaleMode.ScaleToFit, true);
            else GUI.Box(r, "전화기");
            GUI.Label(new Rect(r.center.x - 60, r.y - 24, 120, 24), "전화기", smallStyle);
        }

        Rect SoldierUv()
        {
            if (lastBaseInput.sqrMagnitude < .01f) return new Rect(.078f, .815f, .052f, .145f);
            float[] x = { .079f, .166f, .237f, .310f, .379f, .455f, .529f, .603f };
            float angle = Mathf.Atan2(lastBaseInput.y, lastBaseInput.x) * Mathf.Rad2Deg;
            int frame;
            if (angle > 157.5f || angle <= -157.5f) frame = 2;
            else if (angle > 112.5f) frame = 7;
            else if (angle > 67.5f) frame = 1;
            else if (angle > 22.5f) frame = 6;
            else if (angle > -22.5f) frame = 4;
            else if (angle > -67.5f) frame = 3;
            else if (angle > -112.5f) frame = 0;
            else frame = 5;
            return new Rect(x[frame], .655f, .060f, .165f);
        }

        void DrawFeedback()
        {
            Rect r = new Rect(Screen.width * .20f, Screen.height * .10f, Screen.width * .60f, 92);
            GUI.Box(r, "", panelStyle);
            GUI.Label(new Rect(r.x + 18, r.y + 14, r.width - 36, r.height - 28), feedback, bodyStyle);
        }

        void DrawCover(Texture2D tex, Color tint)
        {
            if (tex != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex, ScaleMode.ScaleAndCrop, true);
            else GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.grayTexture);
            if (tint.a > 0)
            {
                Color old = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }

        Rect NormalizedRect(Vector2 center, float width, float height)
        {
            float x = center.x * Screen.width - width * Screen.width * .5f;
            float y = center.y * Screen.height - height * Screen.height * .5f;
            return new Rect(x, y, width * Screen.width, height * Screen.height);
        }

        static Rect Inset(Rect r, float amount) { return new Rect(r.x + amount, r.y + amount, r.width - amount * 2, r.height - amount * 2); }

        static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        static void DrawRotatedTexture(Rect rect, Texture texture, float angle)
        {
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, rect.center);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.matrix = old;
        }
    }
}

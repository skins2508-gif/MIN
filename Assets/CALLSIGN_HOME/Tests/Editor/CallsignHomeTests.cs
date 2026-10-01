using NUnit.Framework;

namespace CallsignHome.Tests
{
    public sealed class CallsignHomeTests
    {
        [Test]
        public void ChoicesApplyExpectedTradeoffs()
        {
            var state = new RunState();
            state.Reset();
            ChoiceResolver.Apply(state, 0, 1);
            ChoiceResolver.Apply(state, 1, 0);
            ChoiceResolver.Apply(state, 2, 1);
            Assert.AreEqual("연인", state.lastCall);
            Assert.IsTrue(state.helpedMechanic);
            Assert.IsFalse(state.evacuatedWounded);
            Assert.AreEqual(0, state.commandTrust);
        }

        [TestCase(0, "출진")]
        [TestCase(1, "항명")]
        public void FinalChoiceSelectsEnding(int option, string expected)
        {
            var state = new RunState();
            ChoiceResolver.Apply(state, 3, option);
            Assert.AreEqual(expected, state.finalChoice);
        }
    }
}

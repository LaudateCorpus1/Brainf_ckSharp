﻿using Brainf_ck_sharp.NET.Enums;
using Brainf_ck_sharp.NET.Models;
using Brainf_ck_sharp.NET.Models.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Brainf_ck_sharp.NET.Unit
{
    [TestClass]
    public class FunctionTest
    {
        [TestMethod]
        public void SingleCall()
        {
            const string script = "+(,[>+<-]>.)>+:";

            Option<InterpreterResult> result = Brainf_ckInterpreter.TryRun(script, "a");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(result.Value!.ExitCode, ExitCode.TextOutput);
            Assert.AreEqual(result.Value.MachineState.Current.Character, 'a');
            Assert.AreEqual(result.Value.Stdout, "a");
        }

        [TestMethod]
        public void MultipleCalls()
        {
            const string script = "(+++):>:";

            Option<InterpreterResult> result = Brainf_ckInterpreter.TryRun(script);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(result.Value!.ExitCode, ExitCode.NoOutput);
            Assert.AreEqual(result.Value.MachineState[0].Value, 3);
            Assert.AreEqual(result.Value.MachineState[1].Value, 3);
        }

        [TestMethod]
        public void Recursion()
        {
            const string script = ">,<(>[>+<-<:]):>[<<+>>-]<<.[-]";

            Option<InterpreterResult> result = Brainf_ckInterpreter.TryRun(script, "%");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(result.Value!.ExitCode, ExitCode.TextOutput);
            Assert.AreEqual(result.Value.MachineState.Current.Value, 0);
            Assert.AreEqual(result.Value.Stdout, "%");
        }
    }
}
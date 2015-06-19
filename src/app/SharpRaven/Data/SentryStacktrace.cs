#region License

// Copyright (c) 2014 The Sentry Team and individual contributors.
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted
// provided that the following conditions are met:
// 
//     1. Redistributions of source code must retain the above copyright notice, this list of
//        conditions and the following disclaimer.
// 
//     2. Redistributions in binary form must reproduce the above copyright notice, this list of
//        conditions and the following disclaimer in the documentation and/or other materials
//        provided with the distribution.
// 
//     3. Neither the name of the Sentry nor the names of its contributors may be used to
//        endorse or promote products derived from this software without specific prior written
//        permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
// IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
// ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

namespace SharpRaven.Data
{
    /// <summary>
    /// Represents Sentry's version of an <see cref="Exception"/>'s stack trace.
    /// </summary>
    public class SentryStacktrace
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SentryStacktrace"/> class.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/>.</param>
        public SentryStacktrace(Exception exception)
        {
            if (exception == null)
                return;

            Frames = GetStackFrames(exception);
        }


#if PCL

        // These methods are here to facilitate PCL libraries. They are required because the PCL version of System.Exception is missing
        // a lot of functionality. You cannot get each stack frame for example.


        /// <summary>
        /// This method tries to retrieve stack frames from an exception for the PCL code path. It will first try using dynamic and reflection. If this fails
        /// it will fall back to parsing the stack trace, which is a less than ideal solution.
        /// </summary>
        /// <param name="exception">The Exception to retrieve a stack trace from.</param>
        /// <returns></returns>
        [Pure]
        private static ExceptionFrame[] GetStackFrames(Exception exception)
        {
            // First we check if the StackTrace type is available in the environments mscorlib.
            // if not we defer to the parsing strategy.
            var stackTraceType = Type.GetType("System.Diagnostics.StackTrace, mscorlib");

            if (stackTraceType != null)
            {
                try
                {
                    return GetStackframesUsingStackTrace(stackTraceType, exception).ToArray();
                }
                catch (Exception ex)
                {
                    // If an exception happens we will fall through and do a regex parse.
                    Debug.WriteLine("[ERROR] Unable to get exception stack trace! Trying with a regex match.");
                    Debug.WriteLine("[ERROR] " + ex.GetType().Name + ": " + ex.Message);
                }
            }
            try
            {
                return GetStackframesUsingRegularExpressions(exception).ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ERROR] Unable to get exception stack trace!");
                Debug.WriteLine("[ERROR] " + ex.GetType().Name + ": " + ex.Message);
                return new ExceptionFrame[0];
            }
        }

        private static readonly System.Text.RegularExpressions.Regex stackFrameRegex = new System.Text.RegularExpressions.Regex("at (?<Source>.*?) (in (?<Filename>.*?):line (?<LineNumber>[0-9]+))?");


        /// <summary>
        /// This method tries to parse the stack trace of an exception. This is a very un-ideal solution that is hopefully never actually in use.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        [Pure]
        private static IEnumerable<ExceptionFrame> GetStackframesUsingRegularExpressions(Exception exception)
        {
            // This is an unstable way to do this, but I couldn't think of a better approach.
            var match = stackFrameRegex.Match(exception.StackTrace);
            var frames = new List<ExceptionFrame>();
            while (match.Success)
            {
                string filename;
                int lineNumber;

                var fn = match.Groups["Filename"];
                filename = fn.Success ? fn.Value : null;

                var ln = match.Groups["LineNumber"];
                lineNumber = ln.Success ? int.Parse(ln.Value) : 0;

                frames.Add(new ExceptionFrame
                {
                    Function = match.Groups["Source"].Value,
                    Source = match.Groups["Filename"].Value,
                    Filename = filename,
                    LineNumber = lineNumber
                });
                match = match.NextMatch();

            }
            return frames;
        }

        /// <summary>
        /// This method tries to retrieve stack frames from an exception using the systems StactTrace type located in mscorlib.
        /// </summary>
        /// <param name="stackTraceType"></param>
        /// <param name="exception"></param>
        /// <returns></returns>
        [Pure]
        private static IEnumerable<ExceptionFrame> GetStackframesUsingStackTrace(Type stackTraceType, Exception exception)
        {
            // Use dynamic dispatch to evaluate methods at runtime.
            dynamic stackTrace = Activator.CreateInstance(stackTraceType, exception, true);

            var stackFrames = (dynamic[])stackTrace.GetFrames();

            return stackFrames.Select(x =>
            {
                var m = x.GetMethod();
                
                string asmName;

                if (m.DeclaringType != null)
                {
                    var asm = (Assembly)m.DeclaringType.Assembly;

                    var title =
                        asm.GetCustomAttributes(typeof(AssemblyTitleAttribute), false).OfType<AssemblyTitleAttribute>()
                           .FirstOrDefault();

                    if (title != null)
                        asmName = title.Title;
                    else
                        asmName = asm.FullName;
                }
                else
                    asmName = null;

                return new ExceptionFrame
                {
                    Filename = x.GetFileName(),
                    Function = GetMethodName(m),
                    ColumnNumber = x.GetFileColumnNumber(),
                    LineNumber = x.GetFileLineNumber(),
                    Module = m.Module.Name,
                    Source = asmName
                };

            }).ToArray();
        }

        [Pure]
        private static string GetMethodName(MethodBase m)
        {
            if (m.DeclaringType == null)
            {
                return string.Format("{0}({1})",
                          m.Name,
                          string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name)));
            }
            return string.Format("{0}.{1}({2})",
                          m.DeclaringType.FullName,
                          m.Name,
                          string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name)));
        }

#else
        [Pure]
        private static ExceptionFrame[] GetStackFrames(Exception exception)
        {
            StackTrace trace = new StackTrace(exception, true);
            var frames = trace.GetFrames();

            if (frames == null)
                return null;

            int length = frames.Length;
            var resultFrames = new ExceptionFrame[length];

            for (int i = 0; i < length; i++)
            {
                StackFrame frame = trace.GetFrame(i);
                resultFrames[i] = new ExceptionFrame(frame);
            }
            return resultFrames;
        }
#endif


        /// <summary>
        /// Gets or sets the <see cref="Exception"/> frames.
        /// </summary>
        /// <value>
        /// The <see cref="Exception"/> frames.
        /// </value>
        [JsonProperty(PropertyName = "frames")]
        public ExceptionFrame[] Frames { get; set; }


        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            if (Frames == null || !Frames.Any())
                return String.Empty;

            StringBuilder sb = new StringBuilder();

            foreach (var frame in Frames)
            {
                sb.Append("   at ");
                sb.Append(frame);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
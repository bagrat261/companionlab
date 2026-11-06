using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeOllamaPlanning(List<string> checks)
        {
            string before = Brain.Ollama;
            Func<string> gameBefore = Computer.GameOpen;
            List<string> requests = new List<string>(), executed = new List<string>();
            TcpListener listener;
            Brain.Ollama = FakeServer(new[] {
                "{\"message\":{\"role\":\"assistant\",\"content\":\"Открыто три окна.\"},\"done\":true}",
                "{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"function\":{\"name\":\"list_windows\",\"arguments\":{}}}]},\"done\":true}",
                "{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"function\":{\"name\":\"check_app\",\"arguments\":{\"name\":\"companionlab-missing-test\"}}}]},\"done\":true}",
                "{\"message\":{\"role\":\"assistant\",\"content\":\"The requested app is unavailable.\"},\"done\":true}"
            }, requests, out listener);
            try
            {
                Computer.GameOpen = delegate { return null; };
                Brain planner = new Brain { Model = "test-local-tools", OwnSteps = true, OllamaSteps = true };
                planner.Act = delegate(string tool, ToolArguments args)
                {
                    executed.Add(tool);
                    if (tool == "list_windows") return "COMPANIONLAB_WINDOWS_RESULT";
                    if (tool == "check_app" && args.Name == "companionlab-missing-test") return "Couldn't find that app. COMPANIONLAB_APP_MISSING";
                    throw new Exception("Unexpected tool in local planner regression: " + tool);
                };
                string reply = planner.Ask("Show me the desktop and check the required application.");
                if (string.Join(",", executed.ToArray()) != "list_windows,check_app" || requests.Count != 4 || string.IsNullOrWhiteSpace(reply) || planner.History.Count != 2)
                    throw new Exception("The native Ollama planner did not execute two ordered tools and finish one conversation turn.");
                if (requests[0].IndexOf("\"tools\":", StringComparison.Ordinal) < 0 || requests[2].IndexOf("COMPANIONLAB_WINDOWS_RESULT", StringComparison.Ordinal) < 0 || requests[3].IndexOf("COMPANIONLAB_APP_MISSING", StringComparison.Ordinal) < 0)
                    throw new Exception("Native tool schemas or tool results were lost between Ollama requests.");
                checks.Add("PASS Ollama planning: two native tool rounds, real HTTP serialization, ordered execution, failure result returned to model and one retained conversation turn");
            }
            finally
            {
                listener.Stop();
                Brain.Ollama = before;
                Computer.GameOpen = gameBefore;
            }
        }
    }
}

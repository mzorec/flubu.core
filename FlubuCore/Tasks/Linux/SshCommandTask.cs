﻿using FlubuCore.Context;
using Renci.SshNet;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FlubuCore.Tasks.Linux
{
    public class SshCommandTask : TaskBase<int, SshCommandTask>
    {
        private readonly List<string> _commands = new List<string>();
        private readonly string _host;
        private readonly string _userName;
        private string _password;

        public SshCommandTask(string host, string userName)
        {
            _host = host;
            _userName = userName;
        }

        public SshCommandTask(string host, string userName, string password)
        {
            _host = host;
            _userName = userName;
            _password = password;
        }

        public SshCommandTask WithCommand(string command)
        {
            _commands.Add(command);
            return this;
        }

        public SshCommandTask WithPassword(string password)
        {
            _password = password;
            return this;
        }

        protected override string Description { get; set; }

        protected override int DoExecute(ITaskContextInternal context)
        {
            Task<int> task = DoExecuteAsync(context);

            return task.GetAwaiter().GetResult();
        }

        protected override async Task<int> DoExecuteAsync(ITaskContextInternal context)
        {
            context.LogInfo($"Connecting to host {_userName}@{_host}");

            string password = _password.GetPassword();

            using (SshClient client = new SshClient(_host, _userName, password))
            {
                client.Connect();

                StringBuilder cmdText = new StringBuilder();

                foreach (string command in _commands)
                {
                    context.LogInfo($"Executing command {command}");
                    cmdText.Append($"{command} &&");
                }

                cmdText.Remove(cmdText.Length - 2, 2);

                using (SshCommand cmd = client.CreateCommand(cmdText.ToString()))
                {
                    Task<string> task = Task.Factory.FromAsync<string, string>((p, c, st) => cmd.BeginExecute(p, c, st),
                                        cmd.EndExecute, cmdText.ToString(), null);

                    StreamReader reader = new StreamReader(cmd.OutputStream);

                    while (true)
                    {
                        if (task.Wait(1000))
                        {
                            context.LogInfo($"Command response [{task.Result ?? cmd.Error}]");
                            break;
                        }

                        string data = await reader.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(data))
                        {
                            context.LogInfo(data);
                        }
                    }

                    if (!string.IsNullOrEmpty(cmd.Error))
                        context.LogError(cmd.Error);
                }

                client.Disconnect();
                return 0;
            }
        }
    }
}
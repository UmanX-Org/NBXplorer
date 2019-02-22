﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace NBXplorer.NodeWaiter
{
	public abstract class ExitForwarder
	{
		public Process ChildProcess { get; }

		public static ExitForwarder ForwardToChild(Process childProcess)
		{
			try
			{
				var pid = Mono.Unix.UnixProcess.GetCurrentProcessId();
				return new LinuxForwarder(childProcess);
			}
			catch
			{
				return new WindowsForwarder(childProcess);
			}
		}

		public ExitForwarder(Process childProcess)
		{
			ChildProcess = childProcess;
		}
		class LinuxForwarder : ExitForwarder
		{
			public LinuxForwarder(Process childProcess) : base(childProcess)
			{

			}
			public override int WaitForExitAndForward()
			{
				Mono.Unix.Native.Signum[] exitedSignals = null;
				using (var sigint = new Mono.Unix.UnixSignal(Mono.Unix.Native.Signum.SIGINT))
				using (var sigchld = new Mono.Unix.UnixSignal(Mono.Unix.Native.Signum.SIGCHLD))
				using (var sigterm = new Mono.Unix.UnixSignal(Mono.Unix.Native.Signum.SIGTERM))
				{
					var sigs = new[] { sigint, sigterm, sigchld };
					Mono.Unix.UnixSignal.WaitAny(sigs);
					exitedSignals = sigs.Where(s => s.IsSet).Select(s => s.Signum).ToArray();
				}

				var childKill = exitedSignals.Contains(Mono.Unix.Native.Signum.SIGCHLD);
				if (ChildProcess != null)
				{
					if (!childKill)
					{
						foreach (var signal in exitedSignals)
						{
							Mono.Unix.Native.Syscall.kill(ChildProcess.Id, signal);
						}
					}
					ChildProcess.WaitForExit();
					return Mono.Unix.Native.Syscall.WEXITSTATUS(ChildProcess.ExitCode);
				}
				return 0;
			}
		}
		class WindowsForwarder : ExitForwarder
		{
			
			public WindowsForwarder(Process childProcess) : base(childProcess)
			{
			}

			public override int WaitForExitAndForward()
			{
				AutoResetEvent stop = new AutoResetEvent(false);
				Console.CancelKeyPress += (s, e) => Catch(() => stop.Set());
				System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += (s) => Catch(() => stop.Set());
				AppDomain.CurrentDomain.ProcessExit += (s, e) => Catch(() => stop.Set());

				bool childStopped = false;
				if(ChildProcess != null)
				{
					new Thread(() =>
					{
						try
						{
							ChildProcess.WaitForExit();
							stop.Set();
							childStopped = true;
						}
						catch { }
					}).Start();
				}
				stop.WaitOne();

				if(childStopped)
				{
					return ChildProcess.ExitCode;
				}

				if (ChildProcess != null)
				{
					if (!ChildProcess.HasExited)
					{
						ChildProcess.Kill();
						ChildProcess.WaitForExit();
					}
				}
				return 0;
			}
			
			static void Catch(Action act)
			{
				try
				{
					act();
				}
				catch { }
			}
		}
		public abstract int WaitForExitAndForward();
	}
}

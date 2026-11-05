using System;
using System.Threading;

namespace NinjaTraderAutoLogin
{
    internal sealed class LoginEngine
    {
        private readonly ILoginUi ui;
        private readonly ICredentials credentials;
        private readonly IClock clock;
        private readonly IRunEvidence evidence;
        private readonly TimeSpan timeout;
        private readonly TradingMode requestedMode;

        public LoginEngine(ILoginUi ui, ICredentials credentials, IClock clock, IRunEvidence evidence, TimeSpan timeout, TradingMode requestedMode = TradingMode.Simulation)
        {
            this.ui = ui;
            this.credentials = credentials;
            this.clock = clock;
            this.evidence = evidence;
            this.timeout = timeout;
            this.requestedMode = requestedMode;
        }

        public LoginResult Run(bool wasAlreadyRunning, CancellationToken cancellation)
        {
            var result = new LoginResult { RequestedMode = requestedMode };
            var stage = Stage.WaitingForLogin;
            DateTime deadline = clock.UtcNow + timeout;
            // Windows can overlap briefly while NinjaTrader changes screens. These only
            // delay observation; no action is ever repeated.
            var ambiguity = new ProblemGrace(TimeSpan.FromSeconds(3));
            var loginReturned = new ProblemGrace(TimeSpan.FromSeconds(5));
            try
            {
                RequireEvidence(evidence.Event(stage, "Started", null));
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (clock.UtcNow >= deadline)
                    {
                        throw new LoginProblem(Outcome.TimedOut);
                    }

                    UiSnapshot snapshot = ui.Inspect();
                    cancellation.ThrowIfCancellationRequested();
                    if (clock.UtcNow >= deadline)
                    {
                        throw new LoginProblem(Outcome.TimedOut);
                    }
                    // Captures contain control metadata only. No values, labels, or credentials.
                    RequireEvidence(evidence.Event(stage, "Observed", snapshot));
                    if (snapshot.Ambiguous)
                    {
                        if (ambiguity.Expired(clock.UtcNow))
                        {
                            throw new LoginProblem(Outcome.UnsupportedUi);
                        }

                        clock.Wait(cancellation);
                        continue;
                    }

                    ambiguity.Clear();
                    if (snapshot.AuthenticationError)
                    {
                        throw new LoginProblem(Outcome.AuthenticationFailed);
                    }

                    if (snapshot.ManualRequired)
                    {
                        throw new LoginProblem(Outcome.NeedsManual);
                    }

                    if (requestedMode == TradingMode.Live && snapshot.LiveAccountUnavailable)
                    {
                        throw new LoginProblem(Outcome.LiveAccountUnavailable);
                    }

                    if (snapshot.ControlCenter)
                    {
                        if ((requestedMode == TradingMode.Simulation && result.SimulationSelections == 1 && result.LiveSelections == 0) ||
                            (requestedMode == TradingMode.Live && result.LiveSelections == 1 && result.SimulationSelections == 0))
                        {
                            result.Outcome = Outcome.Success;
                        }
                        else if (wasAlreadyRunning && result.LoginSubmissions == 0)
                        {
                            result.Outcome = Outcome.AlreadyOpen;
                        }
                        else
                        {
                            result.Outcome = Outcome.ModeNotConfirmed;
                        }

                        break;
                    }
                    if (stage == Stage.WaitingForTradingMode)
                    {
                        if (!snapshot.LoginReady)
                        {
                            loginReturned.Clear();
                        }
                        else if (loginReturned.Expired(clock.UtcNow))
                        {
                            throw new LoginProblem(Outcome.LoginNotAccepted);
                        }
                    }

                    if (stage == Stage.WaitingForLogin)
                    {
                        if (ModeReady(snapshot))
                        {
                            // An existing, already-authenticated mode dialog can be continued.
                            SelectRequestedMode(stage, result, cancellation);
                            stage = Stage.WaitingForControlCenter;
                            deadline = clock.UtcNow + timeout;
                        }
                        else if (snapshot.LoginReady)
                        {
                            using (Secret secret = credentials.Read())
                            {
                                if (secret == null || !secret.Valid)
                                {
                                    throw new LoginProblem(Outcome.CredentialsMissing);
                                }

                                cancellation.ThrowIfCancellationRequested();
                                RequireEvidence(evidence.Event(stage, "BeforeSubmit", null));
                                ui.Submit(secret, cancellation);
                            }
                            result.LoginSubmissions++;
                            stage = Stage.WaitingForTradingMode;
                            deadline = clock.UtcNow + timeout;
                            RequireEvidence(evidence.Event(stage, "LoginSubmitted", null));
                        }
                    }
                    else if (stage == Stage.WaitingForTradingMode && ModeReady(snapshot))
                    {
                        SelectRequestedMode(stage, result, cancellation);
                        stage = Stage.WaitingForControlCenter;
                        deadline = clock.UtcNow + timeout;
                    }
                    clock.Wait(cancellation);
                }
            }
            catch (OperationCanceledException) { result.Outcome = Outcome.Cancelled; }
            catch (LoginProblem ex)
            {
                result.Outcome = ex.Outcome;
                // Keep the type behind a stop, so a wrong "closed" or "denied" can be traced later.
                if (ex.InnerException != null)
                {
                    evidence.Cause(ex.InnerException);
                }
            }
            catch (Exception error)
            {
                result.Outcome = Outcome.UnexpectedFailure;
                evidence.Cause(error);
            }
            if (!evidence.Complete(result.Outcome, result.LoginSubmissions, result.SimulationSelections, result.LiveSelections))
            {
                result.Outcome = Outcome.EvidenceUnavailable;
            }

            return result;
        }

        private static void RequireEvidence(bool written)
        {
            if (!written)
            {
                throw new LoginProblem(Outcome.EvidenceUnavailable);
            }
        }
        private bool ModeReady(UiSnapshot snapshot)
        {
            return requestedMode == TradingMode.Live ? snapshot.LiveReady : snapshot.SimulationReady;
        }
        private void SelectRequestedMode(Stage stage, LoginResult result, CancellationToken cancellation)
        {
            RequireEvidence(evidence.Event(stage, requestedMode == TradingMode.Live ? "BeforeLive" : "BeforeSimulation", null));
            ui.SelectMode(requestedMode, cancellation);
            if (requestedMode == TradingMode.Live)
            {
                result.LiveSelections++;
            }
            else
            {
                result.SimulationSelections++;
            }

            RequireEvidence(evidence.Event(Stage.WaitingForControlCenter, requestedMode == TradingMode.Live ? "LiveInvoked" : "SimulationInvoked", null));
        }
    }
}

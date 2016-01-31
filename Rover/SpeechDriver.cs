using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SR = Windows.Media.SpeechRecognition;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Rover
{
    class SpeechDriver
    {
        public class SpeechIntentArgs
        {
            public string Intent { private set; get; }
            public Dictionary<string, string> Slots;
            public SpeechIntentArgs(string intent)
            {
                Intent = intent;
                Slots = new Dictionary<string, string>();
            }
            public SpeechIntentArgs(string intent, Dictionary<string, string> slots)
            {
                Intent = intent;
                Slots = slots;
            }
        }

        private SR.SpeechRecognizer _speechRecognizer;
        private SR.ISpeechRecognitionConstraint _enableSpeechConstraint;
        private SR.ISpeechRecognitionConstraint _commandingConstraint;
        
        public bool IsActive { private set; get; }

        public event EventHandler<bool> Activated;
        public event EventHandler<SpeechIntentArgs> SpeechCommandTriggered;

        public async Task InitializeAsync()
        {
            if (_speechRecognizer == null)
            {
                _speechRecognizer = new Windows.Media.SpeechRecognition.SpeechRecognizer();
                _speechRecognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromMilliseconds(300);
                _speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;
                _speechRecognizer.RecognitionQualityDegrading += SpeechRecognizer_RecognitionQualityDegrading;
                _speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
                _speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
                await SetConstraintsAsync();
            }
        }

        public async Task StartAsync()
        {
            await _speechRecognizer.ContinuousRecognitionSession.StartAsync();
        }
        public async Task StopAsync()
        {
            await _speechRecognizer.ContinuousRecognitionSession.CancelAsync();
        }


        private async Task SetConstraintsAsync()
        {
            StorageFile roverCommandsActivationFile = await Package.Current.InstalledLocation.GetFileAsync("SRGS\\RoverCommandsActivation.grxml");
            StorageFile roverCommandsFile = await Package.Current.InstalledLocation.GetFileAsync("SRGS\\RoverCommands.grxml");
            _enableSpeechConstraint = new SR.SpeechRecognitionGrammarFileConstraint(
                                roverCommandsActivationFile,
                                "EnableCommanding");

            _commandingConstraint = new SR.SpeechRecognitionGrammarFileConstraint(
                                roverCommandsFile,
                                "Commands");

            Debug.WriteLine($"Activation file path: {roverCommandsActivationFile.Path}");
            Debug.WriteLine($"Commands file path: {roverCommandsFile.Path}");

            _speechRecognizer.Constraints.Add(_enableSpeechConstraint);
            _speechRecognizer.Constraints.Add(_commandingConstraint);
            _commandingConstraint.IsEnabled = false;
            var status = await _speechRecognizer.CompileConstraintsAsync();
            Debug.WriteLine($"Compilation ended with: {status.Status}");
        }

        private void ContinuousRecognitionSession_ResultGenerated(SR.SpeechContinuousRecognitionSession sender, SR.SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            if (args.Result.Status == SR.SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("Result Text:{0}, Confidence:{1}({2:F4}), Constraint:{3})",
                                args.Result.Text,
                                args.Result.Confidence,
                                args.Result.RawConfidence,
                                (args.Result.Constraint != null) ? args.Result.Constraint.Tag : "<No tag>");
                if (
                    (args.Result.Confidence == SR.SpeechRecognitionConfidence.High) || 
                    (args.Result.Confidence == SR.SpeechRecognitionConfidence.Medium))
                    {
                        switch (args.Result.Constraint.Tag)
                        {
                            case "EnableCommanding":
                                ProcessSpeechStatusChangeCommand(args.Result);
                                break;

                            case "Commands":
                                ProcessSpeechCommand(args.Result);
                                break;

                            default:
                                Debug.WriteLine($"**** Unknown constraint '{args.Result.Constraint.Tag}'");
                                break;
                        }
                    }
            }
            else
            {
                Debug.WriteLine("Result: status({0})", args.Result.Status);
            }
        }

        private void ProcessSpeechStatusChangeCommand(SR.SpeechRecognitionResult result)
        {
            bool activationStatus = IsActive;
            if (result.SemanticInterpretation.Properties.Keys.Contains("action"))
            {
                var intent = result.SemanticInterpretation.Properties["action"][0];
                Debug.WriteLine($"--> Voice reco status change request: {intent}");

                switch (intent)
                {
                    case "ActivateSpeechReco":
                        IsActive = true;
                        break;

                    case "StopSpeechReco":
                        IsActive = false;
                        break;


                    default:
                        break;
                }

                if (activationStatus != IsActive)
                {
                    _commandingConstraint.IsEnabled = IsActive;

                    // Rise the event
                    Activated(this, IsActive);
                }
            }
        }

        private void ProcessSpeechCommand(SR.SpeechRecognitionResult result)
        {
            if (result.SemanticInterpretation.Properties.Keys.Contains("action"))
            {
                var intentArgs = new SpeechIntentArgs(result.SemanticInterpretation.Properties["action"][0]);
                Debug.WriteLine($"--> Voice reco intent: {intentArgs.Intent}");
                // Execute the event
                SpeechCommandTriggered(this, intentArgs);
            }
        }

        private void ContinuousRecognitionSession_Completed(SR.SpeechContinuousRecognitionSession sender, SR.SpeechContinuousRecognitionCompletedEventArgs args)
        {
            Debug.WriteLine($"--> Speech reco session completed: {args.Status}");
            if (IsActive)
            {
                IsActive = false;
                Activated(this, IsActive);
            }
        }

        private void SpeechRecognizer_RecognitionQualityDegrading(SR.SpeechRecognizer sender, SR.SpeechRecognitionQualityDegradingEventArgs args)
        {
            Debug.WriteLine($"- Speech quality: {args.Problem}");
        }

        private void SpeechRecognizer_StateChanged(SR.SpeechRecognizer sender, SR.SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine($"- {args.State}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Net.Http;
using Windows.ApplicationModel;
using Windows.Devices.Gpio;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Globalization;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RPiVoice
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        // Speech Recognizer
        private SpeechRecognizer recognizer;
        
        private const int PUMP_PIN = 24;
        private const int BLOW_INTERVAL = 2000;

        private bool isBlowing = false;
        // GPIO 
        private static GpioController gpio = null;
        // GPIO Pin for RED Led
        private static GpioPin dictatorPin = null;
        private DispatcherTimer tmr = null;

        // The speech recognizer used throughout this sample.
        private SpeechRecognizer speechRecognizer;

        // Keep track of existing text that we've accepted in ContinuousRecognitionSession_ResultGenerated(), so
        // that we can combine it and Hypothesized results to show in-progress dictation mid-sentence.
        private StringBuilder dictatedTextBuilder;


        public MainPage()
        {
            this.InitializeComponent();

            Unloaded += MainPage_Unloaded;

            // Initialize Recognizer
            initializeGPIO();
            initializeDictator();
        }


        // Release resources, stop recognizer, release pins, etc...
        private async void MainPage_Unloaded(object sender, object args)
        {
            // Stop recognizing
            await recognizer.ContinuousRecognitionSession.StopAsync();

            // Release pins
            dictatorPin = null;
            recognizer.Dispose();

            recognizer = null;
        }

        private void initializeGPIO()
        {
            // Initialize GPIO controller
            gpio = GpioController.GetDefault();

            // Initialize GPIO Pins
            dictatorPin = gpio.OpenPin(PUMP_PIN);
            dictatorPin.SetDriveMode(GpioPinDriveMode.Output);

            // Write low initially, this step is not needed
            //dictatorPin.Write(GpioPinValue.Low);
            dictatorPin.Write(GpioPinValue.High);

            //tmr = new DispatcherTimer();
            //tmr.Interval = TimeSpan.FromMilliseconds(BLOW_INTERVAL);
            //tmr.Tick += dictatorTimerHandle; 

        }
        // Control Gpio Pins
        private async void blow()
        {
           await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                isBlowing = true;
                //dictatorPin.Write(GpioPinValue.High);
                dictatorPin.Write(GpioPinValue.Low);

                //tmr.Start();
                MyMediaElement.Play();
            });
        }


        private async void blowStop()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                isBlowing = false;
                dictatorPin.Write(GpioPinValue.High);
                MyMediaElement.Stop();
            });
        }

        // Initialize Speech Recognizer and start async recognition
        private async void initializeDictator()
        {
            if (dictatedTextBuilder != null)
            {
                dictatedTextBuilder.Clear();
            }
            else
            {
                dictatedTextBuilder = new StringBuilder();
            }


            // Initialize recognizer
            await InitializeRecognizer();


            await speechRecognizer.ContinuousRecognitionSession.StartAsync();

        }
      
        // Recognizer state changed
        private void RecognizerStateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine("Speech recognizer state: " + args.State.ToString());
            //if (args.State.ToString() == "SoundStarted") playme();

        }

        // Initialize Speech Recognizer and start async recognition
        private void playmeStart()
        {
            if(!isBlowing)blow();
        }

        private void playmeStop()
        {
            if(isBlowing)blowStop();
        }
        //private async void dictatortimerhandle(object sender, object e) {
        //    await dispatcher.runasync(windows.ui.core.coredispatcherpriority.normal, () => {
        //        //dictatorpin.write(gpiopinvalue.low);
        //        dictatorpin.write(gpiopinvalue.high);
        //        mymediaelement.stop();
        //        //tmr.stop(); // manually stop timer, or let run indefinitely
        //        isblowing = false;
        //    });
        //}

        /// <summary>
        /// Initialize Speech Recognizer and compile constraints.
        /// </summary>
        /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
        /// <returns>Awaitable task.</returns>
        private async Task InitializeRecognizer()
        {
            // await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            // dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;
                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            this.speechRecognizer = new SpeechRecognizer(SpeechRecognizer.SystemSpeechLanguage);

            // Provide feedback to the user about the state of the recognizer. This can be used to provide visual feedback in the form
            // of an audio indicator to help the user understand whether they're being heard.
            speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

            // Apply the dictation topic constraint to optimize for dictated freeform speech.
            var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
            speechRecognizer.Constraints.Add(dictationConstraint);
            SpeechRecognitionCompilationResult result = await speechRecognizer.CompileConstraintsAsync();
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("Grammar Compilation Failed: " + result.Status.ToString());
                // rootPage.NotifyUser("Grammar Compilation Failed: " + result.Status.ToString(), NotifyType.ErrorMessage);
                //btnContinuousRecognize.IsEnabled = false;
            }

            // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
            // some recognized phrases occur, or the garbage rule is hit. HypothesisGenerated fires during recognition, and
            // allows us to provide incremental feedback based on what the user's currently saying.
            speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
            speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            speechRecognizer.HypothesisGenerated += SpeechRecognizer_HypothesisGenerated;
        }

        /// <summary>
        /// Handle events fired when error conditions occur, such as the microphone becoming unavailable, or if
        /// some transient issues occur.
        /// </summary>
        /// <param name="sender">The continuous recognition session</param>
        /// <param name="args">The state of the recognizer</param>
        private void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                // If TimeoutExceeded occurs, the user has been silent for too long. We can use this to 
                // cancel recognition if the user in dictation mode and walks away from their device, etc.
                // In a global-command type scenario, this timeout won't apply automatically.
                // With dictation (no grammar in place) modes, the default timeout is 20 seconds.
                if (args.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
                {
                    Debug.WriteLine("Automatic Time Out of Dictation");
                    Debug.WriteLine(dictatedTextBuilder.ToString());
                    initializeDictator();

                }
                else
                {
                    Debug.WriteLine("Continuous Recognition Completed: " + args.Status.ToString());

                }
            }
        }

        /// <summary>
        /// While the user is speaking, update the textbox with the partial sentence of what's being said for user feedback.
        /// </summary>
        /// <param name="sender">The recognizer that has generated the hypothesis</param>
        /// <param name="args">The hypothesis formed</param>
        private void SpeechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            string hypothesis = args.Hypothesis.Text;

            // Update the textbox with the currently confirmed text, and the hypothesis combined.
            string textboxContent = dictatedTextBuilder.ToString() + " " + hypothesis + " ...";
            Debug.WriteLine(textboxContent);
            playmeStart();
            // if(textboxContent.Length >= 80)initializeDictator();
            // await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            // {
            //     //dictationTextBox.Text = textboxContent;
            //     //btnClearText.IsEnabled = true;
            // });
        }

        /// <summary>
        /// Handle events fired when a result is generated. Check for high to medium confidence, and then append the
        /// string to the end of the stringbuffer, and replace the content of the textbox with the string buffer, to
        /// remove any hypothesis text that may be present.
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>
        private void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // We may choose to discard content that has low confidence, as that could indicate that we're picking up
            // noise via the microphone, or someone could be talking out of earshot.
            if (args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                dictatedTextBuilder.Append(args.Result.Text + " ");

                Debug.WriteLine(dictatedTextBuilder.ToString());
                // await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                // {
                //     //discardedTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                //     //dictationTextBox.Text = dictatedTextBuilder.ToString();
                //     //btnClearText.IsEnabled = true;
                // });
            }
            else
            {
                // In some scenarios, a developer may choose to ignore giving the user feedback in this case, if speech
                // is not the primary input mechanism for the application.
                // Here, just remove any hypothesis text by resetting it to the last known good.
                // await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                // {
                Debug.WriteLine(dictatedTextBuilder.ToString());
                Debug.WriteLine("SpeechRecognitionConfidence.Low");
                //dictationTextBox.Text = dictatedTextBuilder.ToString();

                string discardedText = args.Result.Text;
                if (!string.IsNullOrEmpty(discardedText))
                {
                    discardedText = discardedText.Length <= 25 ? discardedText : (discardedText.Substring(0, 25) + "...");
                    Debug.WriteLine("Discarded due to low/rejected Confidence: " + discardedText);

                    //discardedTextBlock.Text = "Discarded due to low/rejected Confidence: " + discardedText;
                    //discardedTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                // });
            }
        }

        /// <summary>
        /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
        /// </summary>
        /// <param name="sender">The recognizer that is currently running.</param>
        /// <param name="args">The current state of the recognizer.</param>
        private void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine(args.State.ToString());

            if ((args.State.ToString() == "SoundEnded"))
            { 
                playmeStop();
            }

            //  await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {

            // rootPage.NotifyUser(args.State.ToString(), NotifyType.StatusMessage);
            // });
        }


    }
}
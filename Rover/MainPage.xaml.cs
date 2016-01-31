using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Rover
{
    enum RoverMoveState
    {
        Stopped,
        Forward,
        Backward,
    };

    enum RoverTurnState
    {
        Straight,
        Right,
        Left,
        Play,
        TurnAround,
        Attack
    }

    class RoverState
    {
        public RoverMoveState MoveState {get; set; } = RoverMoveState.Stopped;
        public RoverTurnState TurnState { get; set; } = RoverTurnState.Straight;
        public uint TimerId { get; set; } = 0;

        public RoverState Clone()
        {
            RoverState dest = new RoverState();

            dest.MoveState = MoveState;
            dest.TurnState = TurnState;
            dest.TimerId = TimerId;

            return dest;
        }
        public uint NewTimer()
        {
            TimerId++;
            return TimerId;
        }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    // ReSharper disable once RedundantExtendsListEntry
    public sealed partial class MainPage : Page
    {
        const int leftMotorPin1 = 5;
        const int leftMotorPin2 = 6;
        const int rightMotorPin1 = 27;
        const int rightMotorPin2 = 22;

        const int ultrasonicTrigPin = 23;
        const int ultrasonicEchoPin = 24;

        const int blinkyPin = 18;

        private BlinkyDriver _blinkyLed;
        private BackgroundWorker _worker;
        private CoreDispatcher _dispatcher;
        private SpeechDriver _speechDriver;
        private TwoMotorsDriver _driver;

        private bool _finish;

        private ThreadPoolTimer _timer;
        private RoverState _roverState = new RoverState();
        private object _stateLock = new Object();

        public MainPage()
        {
            InitializeComponent();

            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
        }

        private async void MainPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            _blinkyLed = new BlinkyDriver(blinkyPin);
            _speechDriver = new SpeechDriver();
            await _speechDriver.InitializeAsync();
            _speechDriver.Activated += SpeechDriver_Activated;
            _speechDriver.SpeechCommandTriggered += SpeechCommandTriggered;

            _driver = new TwoMotorsDriver(
                    new Motor(leftMotorPin1, leftMotorPin2),
                    new Motor(rightMotorPin1, rightMotorPin2));
            await _speechDriver.StartAsync();

            _worker = new BackgroundWorker();
            _worker.DoWork += DoWork;
            _worker.RunWorkerAsync();
        }

        private void SpeechCommandTriggered(object sender, SpeechDriver.SpeechIntentArgs e)
        {
            lock(_stateLock)
            {
                switch (e.Intent)
                {
                    case "RightTurn":
                        _roverState.TurnState = RoverTurnState.Right;
                        break;

                    case "LeftTurn":
                        _roverState.TurnState = RoverTurnState.Left;
                        break;

                    case "Play":
                        _roverState.TurnState = RoverTurnState.Play;
                        break;

                    case "Attack":
                        _roverState.TurnState = RoverTurnState.Attack;
                        break;

                    case "TurnAround":
                        _roverState.TurnState = RoverTurnState.TurnAround;
                        break;

                    case "Run":
                    case "Forward":
                        _roverState.MoveState = RoverMoveState.Forward;
                        break;

                    case "Backward":
                        _roverState.MoveState = RoverMoveState.Backward;
                        break;

                    case "Stop":
                        _roverState.MoveState = RoverMoveState.Stopped;
                        break;
                    

                }
            }

            WriteLog($"Voice command received: {e.Intent}");
        }

        private void Timer_Tick(uint timerId, ThreadPoolTimer timer)
        {
            lock (_stateLock)
            {
                if (_roverState.TimerId == timerId)
                {
                    _timer = null;
                    _roverState.MoveState = RoverMoveState.Stopped;
                }
            }
        }


        private void SpeechDriver_Activated(object sender, bool e)
        {
            if (e)
            {
                WriteLog("++++ Speech activated");
                _blinkyLed.Start(250);
            }
            else
            {
                WriteLog("---- Speech deactivated");
                _blinkyLed.Stop();
            }
        }

        private void MainPage_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _finish = true;
        }

        private void ResetTimer(ThreadPoolTimer currentTimer)
        {
            if (currentTimer != null)
            {
                currentTimer.Cancel();
            }

            _timer = null;
        }
        private void ResetTimer(ThreadPoolTimer currentTimer, uint timerId, int durrationMs)
        {
            if (currentTimer != null)
            {
                currentTimer.Cancel();
            }

            _timer = ThreadPoolTimer.CreateTimer(
                (ThreadPoolTimer timer) =>
                {
                    Timer_Tick(timerId, timer);
                },
                TimeSpan.FromMilliseconds(durrationMs));
        }
        private async void DoWork(object sender, DoWorkEventArgs e)
        {
            var ultrasonicDistanceSensor = new UltrasonicDistanceSensor(
                                                            ultrasonicTrigPin,
                                                            ultrasonicEchoPin);
            await ultrasonicDistanceSensor.InitAsync();

            var previousState = _roverState.Clone();
            var state = _roverState.Clone();
            var currentTimer = _timer;
            while (!_finish)
            {
                try
                {
                    lock(_stateLock)
                    {
                        currentTimer = _timer;
                        previousState = state;
                        state = _roverState.Clone();
                    }

                    if (state.TurnState == RoverTurnState.Right)
                    {
                        _roverState.TurnState = RoverTurnState.Straight;
                        await _driver.TurnRightAsync();
                    }
                    else if (state.TurnState == RoverTurnState.Left)
                    {
                        _roverState.TurnState = RoverTurnState.Straight;
                        await _driver.TurnLeftAsync();
                    }
                    else if (state.TurnState == RoverTurnState.TurnAround)
                    {
                        _roverState.TurnState = RoverTurnState.Straight;
                        await _driver.TurnAroundAsync();
                    }
                    else if (state.TurnState == RoverTurnState.Play)
                    {
                        _roverState.TurnState = RoverTurnState.Straight;
                        await _driver.PlayAsync();
                    }
                    else if (state.TurnState == RoverTurnState.Attack)
                    {
                        _roverState.TurnState = RoverTurnState.Straight;
                        await _driver.AttackAsync();
                    }
                    else
                    {
                        if (previousState.MoveState != state.MoveState)
                        {
                            switch (state.MoveState)
                            {
                                case RoverMoveState.Backward:
                                    ResetTimer(currentTimer, _roverState.NewTimer(), 2000);
                                    _driver.MoveBackward();
                                    break;

                                case RoverMoveState.Forward:
                                    ResetTimer(currentTimer, _roverState.NewTimer(), 2000);
                                    _driver.MoveForward();
                                    break;

                                case RoverMoveState.Stopped:
                                    ResetTimer(currentTimer);
                                    _driver.Stop();
                                    break;
                            }
                        }
                    }
                
                    await Task.Delay(20);

                    /*
                    var distance = await ultrasonicDistanceSensor.GetDistanceInCmAsync(1000);
                    WriteData("Forward", distance);

                    if (distance <= 35.0)
                    {
                        WriteLog($"Obstacle found at {distance:F2} cm or less. Turning right");
                        WriteData("Turn Right", distance);

                        await driver.TurnRightAsync();

                        WriteLog("Moving forward");
                    }
                    */
                }
                catch (Exception ex)
                {
                    WriteLog(ex.Message);
                    _driver.Stop();
                    WriteData("Stop", -1);
                }
            }
        }

        private async void WriteLog(string text)
        {
            System.Diagnostics.Debug.WriteLine($"{text}");
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Log.Text += $"{text} | ";
            });
        }

        private async void WriteData(string move, double distance)
        {
            System.Diagnostics.Debug.WriteLine($"{move} {distance} cm");
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Direction.Text = move;
                Distance.Text = $"{distance:F2} cm";
            });
        }
    }
}
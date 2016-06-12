using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows;
using System.IO;
using System.IO.Ports;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime;
using System.Media;

using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Configurations;

using SharpDX.XInput;

using Thorlabs.MotionControl.DeviceManagerCLI;
using Thorlabs.MotionControl.GenericMotorCLI;
using Thorlabs.MotionControl.GenericMotorCLI.ControlParameters; 
using Thorlabs.MotionControl.GenericMotorCLI.AdvancedMotor;
using Thorlabs.MotionControl.GenericMotorCLI.Settings;
using Thorlabs.MotionControl.KCube.DCServoCLI;

using Zaber;
using Zaber.PlugIns;
using Zaber.Serial.Core;


namespace XBoxStage
{

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        DispatcherTimer _timer = new DispatcherTimer();
        private byte[] pingMessage = System.Text.Encoding.ASCII.GetBytes("ping\n");
        
        // Thorlabs Actuator (serial number provided is specific to my device)
        public KCubeDCServo thorDevice;
        public string serialNo = "27000117";

        // Arduino and sensor output stuff
        public SerialPort Arduino = new SerialPort();
        private double _axisMax;
        private double _axisMin;
        public event PropertyChangedEventHandler PropertyChanged;
        public string filename;
        public string path;

        // Zaber
        static public int X_MAX = 1526940;
        static public int Y_MAX = 3149606;
        static public int Z_MAX = 305381;
        int MAX_X_SPEED = mmToSteps("x", 20.0);
        int MAX_Y_SPEED = mmToSteps("y", 20.0);
        int MAX_Z_SPEED = mmToSteps("z", 10.0);
        public ZaberAsciiPort zaberPort;
        public int[] curPos = new int[3];
        public int[] XButtonPosition = new int[3];
        public int[] YButtonPosition = new int[3];
        public int[] BButtonPosition = new int[3];
        public double joystickVelocityModulator = 1.15;
        
        // XBox
        private SharpDX.XInput.State m_xboxState;
        private SharpDX.XInput.State m_xboxStateLast;
        private SharpDX.XInput.Controller m_xbox;
        public int XBOX_MAX_RANGE = 32767;
        public int rDeadZone = Gamepad.RightThumbDeadZone; 
        public int lDeadZone = Gamepad.LeftThumbDeadZone;
        public bool LeftLastStateDeadZone = false;
        public bool LeftCurrentStateDeadZone = false;
        public bool RightLastStateDeadZone = false;
        public bool RightCurrentStateDeadZone = false;
        public bool SimultaneousUpDandBCur = false;
        public bool SimultaneousUpDandBLast = false;
        public bool SimultaneousUpDandXCur = false;
        public bool SimultaneousUpDandXLast = false;
        public bool SimultaneousUpDandYCur = false;
        public bool SimultaneousUpDandYLast = false;
        static public int POLL_RATE = 100; //in ms NOTE: XBox is limited to 30ms minimum
        //For the Classic Buttons
        public event EventHandler OnXBoxGamepadButtonPressA;
        public event EventHandler OnXBoxGamepadButtonPressAOneShot;
        public event EventHandler OnXBoxGamepadButtonPressB;
        public event EventHandler OnXBoxGamepadButtonPressBOneShot;
        public event EventHandler OnXBoxGamepadButtonPressX;
        public event EventHandler OnXBoxGamepadButtonPressXOneShot;
        public event EventHandler OnXBoxGamepadButtonPressY;
        public event EventHandler OnXBoxGamepadButtonPressYOneShot;
        //For D Pad Buttons
        public event EventHandler OnXBoxGamepadButtonPressDUp;
        public event EventHandler OnXBoxGamepadButtonPressDUpOneShot;
        public event EventHandler OnXBoxGamepadButtonPressDDown;
        public event EventHandler OnXBoxGamepadButtonPressDDownOneShot;
        public event EventHandler OnXBoxGamepadButtonPressDLeft;
        public event EventHandler OnXBoxGamepadButtonPressDLeftOneShot;
        public event EventHandler OnXBoxGamepadButtonPressDRight;
        public event EventHandler OnXBoxGamepadButtonPressDRightOneShot;
        //For the Shoulders and ThumbsIn
        public event EventHandler OnXBoxGamepadButtonPressShoulderRight;
        public event EventHandler OnXBoxGamepadButtonPressShoulderRightOneShot;
        public event EventHandler OnXBoxGamepadButtonPressShoulderLeft;
        public event EventHandler OnXBoxGamepadButtonPressShoulderLeftOneShot;
        public event EventHandler OnXBoxGamepadButtonPressRightThumbIn;
        public event EventHandler OnXBoxGamepadButtonPressRightThumbInOneShot;
        public event EventHandler OnXBoxGamepadButtonPressLeftThumbIn;
        public event EventHandler OnXBoxGamepadButtonPressLeftThumbInOneShot;

        // ----------------------------------------------------------------------
        // Property notification boilerplate
        // ----------------------------------------------------------------------
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // The _time_Tick function happens every x ms (set in MainWindow())
        // and polls the gamepad each time
        void _timer_Tick(object sender, EventArgs e)
        {
            PollGamepad();
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            thorDevice.StopPolling();
            thorDevice.Disconnect();
            zaberStop(2);
            zaberStop(3);
            zaberStop(4);
            zaberPort.Close();
            Arduino.Close();
            m_xbox = null;
            
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("attempting to play intro music");
            SoundPlayer rocky = new SoundPlayer();
            rocky.SoundLocation = @"C:\Users\ariaj\Documents\Visual Studio 2015\Projects\XBoxStageTonalButtons\XBoxStageMaster\XBoxStage\Resources\trumpets.wav";
            rocky.Play();
            m_xbox = new Controller(UserIndex.One);
            if (m_xbox.IsConnected) return;
            System.Windows.MessageBox.Show("Controller is not connected");
            App.Current.Shutdown();
        }


        // ----------------------------------------------------------------------
        // MainWindow
        // ----------------------------------------------------------------------
        public MainWindow() 
        {
            
            // This stuff loads the window and closes it
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            //Initialize Controlled Parts
            thorInitialize();
            arduinoInitialize();
            zaberInitialize();

            //A couple Thor things that don't play nice outside of main()
            
            var thorAcceleration = thorDevice.AdvancedMotorLimits.AccelerationMaximum;
            var thorSpeed = thorDevice.AdvancedMotorLimits.VelocityMaximum; //1m;
            var thorVelParams = thorDevice.GetVelocityParams();
            thorVelParams.Acceleration = thorAcceleration;
            thorVelParams.MaxVelocity = thorSpeed;
            thorDevice.SetVelocityParams(thorVelParams);
            thorDevice.StartPolling(20);
            
            // This guy does the gamepad polling every however many ms you want it to. 
            // The higher the sampling rate, the more likely it'll bork. YOU'VE BEEN WARNED!!
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(POLL_RATE) };
            _timer.Tick += _timer_Tick;
            _timer.Start();

            string timeAndDate = GetTimeAndDate(DateTime.Now);
            filename = "SensorOutput" + timeAndDate + ".txt";
            path = @"C:\Users\ariaj\Documents\" + filename;

            String timeStamp = GetTimeStamp(DateTime.Now);
            Console.WriteLine("The time stamp is: {0}", timeStamp);

            var hallMapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y
            var thermMapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y
            var windMapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(hallMapper);
            Charting.For<MeasureModel>(thermMapper);
            Charting.For<MeasureModel>(windMapper);

            //the values property will store our values array
            hallValues = new ChartValues<MeasureModel>();
            thermValues = new ChartValues<MeasureModel>();
            windValues = new ChartValues<MeasureModel>();

            //lets set how to display the X Labels
            DateTimeFormatter = value => new DateTime((long)value).ToString("mm:ss");

            AxisStep = TimeSpan.FromSeconds(5).Ticks;
            SetAxisLimits(DateTime.Now);

            // This timer does the graphing instead
            Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            Timer.Tick += TimerOnTick;
            IsDataInjectionRunning = false;

            DataContext = this;
            Console.WriteLine("Inside of MainWindow()");

            // Do something for this button being pressed
            // Classic Buttons
            OnXBoxGamepadButtonPressA += (s, e) =>
            {
                //Console.WriteLine("ButtonA -- stop all");
                zaberStop(2);
                zaberStop(3);
                zaberStop(4);
                thorDevice.StopImmediate();
                SoundPlayer cancel = new SoundPlayer();
                cancel.SoundLocation = @"C:\Users\ariaj\Documents\Visual Studio 2015\Projects\XBoxStageTonalButtons\XBoxStageMaster\XBoxStage\Resources\buzzer.wav";
                cancel.Play();
                cancel.Dispose();
            };
            // Do something for this button being held
            OnXBoxGamepadButtonPressAOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonAOneShot");
            };

            OnXBoxGamepadButtonPressB += (s, e) =>
            {
                 
                Console.WriteLine("ButtonB Move to designated B position");
                curPos = zaberGetCurrentPos();
                Console.WriteLine("curPos[0]: {0} \t curPos[1]: {1} \t curPos[2]: {2}", curPos[0], curPos[1], curPos[2]);
                Console.WriteLine("BButtonPosition[0]: {0} \t BButtonPosition[1]: {1} \t BButtonPosition[2]: {2}", BButtonPosition[0], BButtonPosition[1], BButtonPosition[2]);
                zaberMoveStoredPositionAllAtOnce(BButtonPosition);
                SoundPlayer a100Hz = new SoundPlayer();
                a100Hz.SoundLocation = @"C:\Users\ariaj\Documents\Visual Studio 2015\Projects\XBoxStageTonalButtons\XBoxStageMaster\XBoxStage\Resources\a100Hz.wav";
                a100Hz.Play();
                a100Hz.Dispose();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(path, true))
                {
                    file.Write("\n");
                    file.Write(GetTimeStamp(DateTime.Now));
                    file.Write(", Button B pressed: move to ({0}, {1}, {2})", BButtonPosition[0], BButtonPosition[1], BButtonPosition[2]);
                    file.Write("\n");
                }
            };
            OnXBoxGamepadButtonPressBOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonBOneShot");
            };

            OnXBoxGamepadButtonPressX += (s, e) =>
            {
                Console.WriteLine("ButtonX: attempt move to designated X position");
                curPos = zaberGetCurrentPos();
                Console.WriteLine("curPos[0]: {0} \t curPos[1]: {1} \t curPos[2]: {2}", curPos[0], curPos[1], curPos[2]);
                Console.WriteLine("XButtonPosition[0]: {0} \t XButtonPosition[1]: {1} \t XButtonPosition[2]: {2}", XButtonPosition[0], XButtonPosition[1], XButtonPosition[2]);
                //zaberMoveStoredPositionOneAtATime(XButtonPosition, curPos);
                zaberMoveStoredPositionAllAtOnce(XButtonPosition);
                SoundPlayer a250Hz = new SoundPlayer();
                a250Hz.SoundLocation = @"C:\Users\ariaj\Documents\Visual Studio 2015\Projects\XBoxStageTonalButtons\XBoxStageMaster\XBoxStage\Resources\a250Hz.wav";
                a250Hz.Play();
                a250Hz.Dispose();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(path, true))
                {
                    file.Write("\n");
                    file.Write(GetTimeStamp(DateTime.Now));
                    file.Write(", Button X pressed: move to ({0}, {1}, {2})", XButtonPosition[0], XButtonPosition[1], XButtonPosition[2]);
                    file.Write("\n");
                }
            };
            OnXBoxGamepadButtonPressXOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonXOneShot");
            };

            OnXBoxGamepadButtonPressY += (s, e) =>
            {
                Console.WriteLine("ButtonY: attempt move to designated Y position");
                curPos = zaberGetCurrentPos();
                Console.WriteLine("curPos[0]: {0} \t curPos[1]: {1} \t curPos[2]: {2}", curPos[0], curPos[1], curPos[2]);
                Console.WriteLine("YButtonPosition[0]: {0} \t YButtonPosition[1]: {1}\t YButtonPosition[2]: {2}", YButtonPosition[0], YButtonPosition[1], YButtonPosition[2]);
                //zaberMoveStoredPositionOneAtATime(YButtonPosition, curPos);
                zaberMoveStoredPositionAllAtOnce(YButtonPosition);
                SoundPlayer a440Hz = new SoundPlayer();
                a440Hz.SoundLocation = @"C:\Users\ariaj\Documents\Visual Studio 2015\Projects\XBoxStageTonalButtons\XBoxStageMaster\XBoxStage\Resources\a440Hz.wav";
                a440Hz.Play();
                a440Hz.Dispose();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(path, true))
                {
                    file.Write("\n");
                    file.Write(GetTimeStamp(DateTime.Now));
                    file.Write(", Button Y pressed: move to ({0}, {1}, {2})", YButtonPosition[0], YButtonPosition[1], YButtonPosition[2]);
                    file.Write("\n");
                }
            };
            OnXBoxGamepadButtonPressYOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonYOneShot");
            };

            // DPad Buttons
            OnXBoxGamepadButtonPressDUp += (s, e) =>
            {
                //Console.WriteLine("ButtonDUp");
            };
            OnXBoxGamepadButtonPressDUpOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonDUpOneShot");
            };

            OnXBoxGamepadButtonPressDDown += (s, e) =>
            {
                Console.WriteLine("ButtonDDown");
            };
            OnXBoxGamepadButtonPressDDownOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonDDownOneShot");
            };

            OnXBoxGamepadButtonPressDLeft += (s, e) =>
            {
                //Console.WriteLine("ButtonDLeft");
                

            };
            OnXBoxGamepadButtonPressDLeftOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonDLeftOneShot");
            };

            OnXBoxGamepadButtonPressDRight += (s, e) =>
            {
                //Console.WriteLine("ButtonDRight");
                
            };
            OnXBoxGamepadButtonPressDRightOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonDRightOneShot");
            };

            // Shoulder and ThumbsIn Buttons
            OnXBoxGamepadButtonPressShoulderRight += (s, e) =>
            {
                Console.WriteLine("ButtonShoulderRight");
                if (Arduino.IsOpen)
                {
                    Arduino.Write("A");
                }
            };
            OnXBoxGamepadButtonPressShoulderRightOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonShoulderRightOneShot");
            };

            OnXBoxGamepadButtonPressShoulderLeft += (s, e) =>
            {
                Console.WriteLine("ButtonShoulderLeft");
            };
            OnXBoxGamepadButtonPressShoulderLeftOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonShoulderLeftOneShot");
            };

            OnXBoxGamepadButtonPressRightThumbIn += (s, e) =>
            {
                //Console.WriteLine("ButtonRightThumbIn");
            };
            OnXBoxGamepadButtonPressRightThumbInOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonRightThumbInOneShot");
            };

            OnXBoxGamepadButtonPressLeftThumbIn += (s, e) =>
            {
                //Console.WriteLine("ButtonLeftThumbIn");
            };
            OnXBoxGamepadButtonPressLeftThumbInOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonLeftThumbInOneShot");
            };
            
        }

        // ----------------------------------------------------------------------
        // XBox Controller Functions
        // ----------------------------------------------------------------------
        // return true if the button state just transitioned from 0 to 1
        // Gives more control to the button
        public void xboxInitialize()
        {
            // If the controller was not assigned to player One during initialization
            // attempt to connect to a player port
            m_xbox = new SharpDX.XInput.Controller(UserIndex.Any);
            if (!m_xbox.IsConnected)
            {
                Console.WriteLine("XBox Controller Could not be connected. Goodbye!");
                System.Threading.Thread.Sleep(2000);
                App.Current.Shutdown();
            }
        }

        private bool ButtonOneShot (GamepadButtonFlags button)
        {
            return !m_xboxStateLast.Gamepad.Buttons.HasFlag(button) && m_xboxState.Gamepad.Buttons.HasFlag(button);
        }

        // return true if the button is pushed 
        private bool ButtonPushed(GamepadButtonFlags button)
        {
            return m_xboxState.Gamepad.Buttons.HasFlag(button);
        }

        public void xboxJoystick(string side, double x, double y)
        {
            Vector joystickInput = new Vector(x, y);
            double magnitude = Math.Sqrt(x * x + y * y);

            int deadzone;
            bool currentStateDeadZone, lastStateDeadZone, isLeftSide;
            int xDevice, yDevice;

            switch (side)
            {
                case "left":
                    currentStateDeadZone = LeftCurrentStateDeadZone;
                    lastStateDeadZone = LeftLastStateDeadZone;
                    deadzone = lDeadZone;
                    isLeftSide = true;
                    xDevice = 2; //corresponds to zaber x-axis
                    yDevice = 3; //corresponds to zaber y-axis
                    break;
                case "right":
                    currentStateDeadZone = RightCurrentStateDeadZone;
                    lastStateDeadZone = RightLastStateDeadZone;
                    deadzone = rDeadZone;
                    isLeftSide = false;
                    xDevice = 0; //this will make it default in zaber move, just returning
                    yDevice = 4; //corresponds to zaber z-axis
                    break;
                default:
                    Console.WriteLine("Joystick side picked is not valid");
                    return;
            }

            if (magnitude > deadzone)
            {
                currentStateDeadZone = false;
                joystickInput.X = (joystickInput.X / magnitude) * ((magnitude - Convert.ToDouble(deadzone)) / (XBOX_MAX_RANGE - Convert.ToDouble(deadzone)));
                joystickInput.Y = (joystickInput.Y / magnitude) * ((magnitude - Convert.ToDouble(deadzone)) / (XBOX_MAX_RANGE - Convert.ToDouble(deadzone)));
                //send the LX and LY values to move calculating function
                if (Math.Abs(x) > deadzone) zaberMoveVelocity(xDevice, joystickInput.X);//x, y, isLeftSide);
                if (Math.Abs(y) > deadzone) zaberMoveVelocity(yDevice, -joystickInput.Y);//-y, x, isLeftSide);
            }
            else
            {
                currentStateDeadZone = true;
                if (lastStateDeadZone == false)
                {
                    zaberStop(xDevice);
                    zaberStop(yDevice);
                }
            }
            if (isLeftSide) LeftCurrentStateDeadZone = currentStateDeadZone;
            else if (!isLeftSide) RightCurrentStateDeadZone = currentStateDeadZone;
        }

        // Main XBox processing
        private void PollGamepad()
        {
            // Update statuses and save buttons
            if ((m_xbox == null) || !m_xbox.IsConnected) return;
            m_xboxStateLast = m_xboxState;
            m_xboxState = m_xbox.GetState();
            LeftLastStateDeadZone = LeftCurrentStateDeadZone;
            RightLastStateDeadZone = RightCurrentStateDeadZone;
            SimultaneousUpDandBLast = SimultaneousUpDandBCur;
            SimultaneousUpDandXLast = SimultaneousUpDandXCur;
            SimultaneousUpDandYLast = SimultaneousUpDandYCur;

            if (m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B)) SimultaneousUpDandBCur = true;
            else SimultaneousUpDandBCur = false;
            if (m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X)) SimultaneousUpDandXCur = true;
            else SimultaneousUpDandXCur = false;
            if (m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y)) SimultaneousUpDandYCur = true;
            else SimultaneousUpDandYCur = false;

            // Event handlers for buttons being pushed
            // Classic Buttons
            if (ButtonPushed(GamepadButtonFlags.A)) OnXBoxGamepadButtonPressA.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.A)) OnXBoxGamepadButtonPressAOneShot.Invoke(this, null);
            //if (ButtonPushed(GamepadButtonFlags.B)) OnXBoxGamepadButtonPressB.Invoke(this, null);
            if (m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B) && !m_xboxStateLast.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B) && !m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)) OnXBoxGamepadButtonPressB.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.B)) OnXBoxGamepadButtonPressBOneShot.Invoke(this, null);
            //if (ButtonPushed(GamepadButtonFlags.X)) OnXBoxGamepadButtonPressX.Invoke(this, null);
            if (m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X) && !m_xboxStateLast.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X) && !m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)) OnXBoxGamepadButtonPressX.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.X)) OnXBoxGamepadButtonPressXOneShot.Invoke(this, null);
            //if (ButtonPushed(GamepadButtonFlags.Y)) OnXBoxGamepadButtonPressY.Invoke(this, null);
            if (m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y) && !m_xboxStateLast.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y) && !m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)) OnXBoxGamepadButtonPressY.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.Y)) OnXBoxGamepadButtonPressYOneShot.Invoke(this, null);

            // Shoulder and Joystick-In buttons
            if (ButtonOneShot(GamepadButtonFlags.RightShoulder)) OnXBoxGamepadButtonPressShoulderRightOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.LeftShoulder)) OnXBoxGamepadButtonPressShoulderLeft.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.LeftShoulder)) OnXBoxGamepadButtonPressShoulderLeftOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.RightThumb)) OnXBoxGamepadButtonPressRightThumbIn.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.RightThumb)) OnXBoxGamepadButtonPressRightThumbInOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.LeftThumb)) OnXBoxGamepadButtonPressLeftThumbIn.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.LeftThumb)) OnXBoxGamepadButtonPressLeftThumbInOneShot.Invoke(this, null);
            
            // D Pad Buttons
            if (ButtonPushed(GamepadButtonFlags.DPadUp)) OnXBoxGamepadButtonPressDUp.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.DPadUp)) OnXBoxGamepadButtonPressDUpOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.DPadDown)) OnXBoxGamepadButtonPressDDown.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.DPadDown)) OnXBoxGamepadButtonPressDDownOneShot.Invoke(this, null);

            var DR = GamepadButtonFlags.DPadRight;
            var DL = GamepadButtonFlags.DPadLeft;
            if (m_xboxState.Gamepad.Buttons.HasFlag(DR) && !m_xboxStateLast.Gamepad.Buttons.HasFlag(DR))
            {
                if (ButtonPushed(DR) && !(ButtonPushed(DL) || ButtonOneShot(DL))) thorMove(thorDevice, MotorDirection.Forward);
            }

            if (m_xboxState.Gamepad.Buttons.HasFlag(DL) && !m_xboxStateLast.Gamepad.Buttons.HasFlag(DL))
            {
                if (ButtonPushed(DL) && !(ButtonPushed(DR) || ButtonOneShot(DR))) thorMove(thorDevice, MotorDirection.Backward);
            }
            if (!m_xboxState.Gamepad.Buttons.HasFlag(DL) && m_xboxStateLast.Gamepad.Buttons.HasFlag(DL))
            {
                thorStop();
            }
            if (!m_xboxState.Gamepad.Buttons.HasFlag(DR) && m_xboxStateLast.Gamepad.Buttons.HasFlag(DR))
            {
                thorStop();
            }

            if (ButtonOneShot(GamepadButtonFlags.DPadLeft)) OnXBoxGamepadButtonPressDLeftOneShot.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.DPadRight)) OnXBoxGamepadButtonPressDRightOneShot.Invoke(this, null);

            // Combos to save positions
            if (SimultaneousUpDandBCur && !SimultaneousUpDandBLast) BButtonPosition = zaberGetCurrentPos();
            if (SimultaneousUpDandXCur && !SimultaneousUpDandXLast) XButtonPosition = zaberGetCurrentPos(); 
            if (SimultaneousUpDandYCur && !SimultaneousUpDandYLast) YButtonPosition = zaberGetCurrentPos(); 

            // Do the triggers and shoulders
            var RTrig = m_xboxState.Gamepad.RightTrigger;
            var LTrig = m_xboxState.Gamepad.LeftTrigger;
            if (RTrig != 0) arduinoSendByte(RTrig);

            // Do the Right and Left Thumb X, Y
            double RX = m_xboxState.Gamepad.RightThumbX;
            double RY = m_xboxState.Gamepad.RightThumbY;
            double LX = m_xboxState.Gamepad.LeftThumbX;
            double LY = m_xboxState.Gamepad.LeftThumbY;

            xboxJoystick("left", LX, LY);
            xboxJoystick("right", RX, RY);
        }

        // ----------------------------------------------------------------------
        // Thorlabs KCube DC Servo Functions
        // ----------------------------------------------------------------------
        public void thorInitialize()
        {
            // Open Thorlabs Actuator
            try
            {
                DeviceManagerCLI.BuildDeviceList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception raised by BuildDeviceList {0}", ex);
                Console.ReadKey();
                return;
            }

            // Get available KCube DC Servo and check if serial number is correct
            DeviceManagerCLI.BuildDeviceList();
            List<string> serialNumbers = DeviceManagerCLI.GetDeviceList(KCubeDCServo.DevicePrefix);
            int devicesCount = serialNumbers.Count();
            string devices = string.Join(",", serialNumbers.ToArray());
            Console.WriteLine("Number of devices found \"{0}\"", devicesCount);
            Console.WriteLine("devices found \"{0}\"", devices);
            if (devices == "") Console.WriteLine("No thor device found!!");
            if (!serialNumbers.Contains(serialNo))
            {
                // the requested serial number is not a TBD001 or is not connected
                Console.WriteLine("{0} is not a valid serial number", serialNo);
                Console.ReadKey();
                return;
            }

            // create the device
            thorDevice = KCubeDCServo.CreateKCubeDCServo(serialNo);
            if (thorDevice == null)
            {
                // an error occured
                Console.WriteLine("{0} is not a KCube DC Servo", serialNo);
                Console.ReadKey();
                return;
            }

            // open a connection to the device.
            try
            {
                Console.WriteLine("Opening device {0}", serialNo);
                thorDevice.Connect(serialNo);
            }
            catch (Exception)
            {
                Console.WriteLine("Failed to open device {0}", serialNo);
                Console.ReadKey();
                return;
            }

            // wait for the device settings to initialize      
            if (!thorDevice.IsSettingsInitialized())
            {
                try
                {
                    thorDevice.WaitForSettingsInitialized(5000);
                    if (thorDevice.IsSettingsInitialized()) Console.WriteLine("ThorDevice Should be Initialized");
                }
                catch (Exception)
                {
                    Console.WriteLine("Settings failed to initialize");
                }
            }

            // call GetMotorConfiguration on the device to initialize the DeviceUnitConverter object required for real world unit parameters  
            MotorConfiguration motorSettings = thorDevice.GetMotorConfiguration(serialNo);

            // display info about device     
            DeviceInfo deviceInfo = thorDevice.GetDeviceInfo();
            Console.WriteLine("Device {0} = {1}", deviceInfo.SerialNumber, deviceInfo.Name);

        }

        public void thorMove(IGenericAdvancedMotor device, MotorDirection direction )
        {
            try
            {
                device.MoveContinuous(direction);
            }
            catch (Exception e)
            {
                Console.WriteLine(Convert.ToString(e));
                Console.WriteLine("Failed to move to position");
                return;
            }
        }

        public void thorStop()
        {
            Action<UInt64> workDone = thorDevice.InitializeWaitHandler();
            thorDevice.Stop(workDone);            
            thorDevice.ResumeMoveMessages();
        }

        // ----------------------------------------------------------------------
        // Arduino Functions
        // ----------------------------------------------------------------------
        // arduinoInitialize() opens the connection to the arduino
        public void arduinoInitialize()
        {
            Arduino.PortName = "COM6";
            Arduino.BaudRate = 9600;
            Arduino.Open();
        }

        // writeToArduino writes a command to the arduino
        void writeToArduino(string command)
        {
            //Send Trigger value to arduino
            if (Arduino.IsOpen)
            {
                Arduino.Write(command);
            }
        }

        private void arduinoSendByte(byte byteToSend)
        {
            byte[] bytes = new byte[] { byteToSend };
            Arduino.Write(bytes, 0, bytes.Length);
        }

        public class MeasureModel
        {
            public DateTime DateTime { get; set; }
            public double Value { get; set; }
        }

        public ChartValues<MeasureModel> hallValues { get; set; }
        public ChartValues<MeasureModel> thermValues { get; set; }
        public ChartValues<MeasureModel> windValues { get; set; }

        public Func<double, string> DateTimeFormatter { get; set; }

        public double AxisStep { get; set; }

        public double AxisMax
        {
            get { return _axisMax; }
            set
            {
                _axisMax = value;
                OnPropertyChanged("AxisMax");
            }
        }
        public double AxisMin
        {
            get { return _axisMin; }
            set
            {
                _axisMin = value;
                OnPropertyChanged("AxisMin");
            }
        }

        public DispatcherTimer Timer { get; set; }
        public bool IsDataInjectionRunning { get; set; }

        private void RunDataOnClick(object sender, RoutedEventArgs e)
        {
            if (IsDataInjectionRunning)
            {
                Timer.Stop();
                IsDataInjectionRunning = false;
            }
            else
            {
                Timer.Start();
                IsDataInjectionRunning = true;
            }
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            var now = DateTime.Now;
            string hallReply = "";
            string thermReply = "";
            string windReply = "";

            if (Arduino.IsOpen)
            {
                hallReply = Arduino.ReadTo(";");
                thermReply = Arduino.ReadTo(";");
                windReply = Arduino.ReadTo(";");
                hallValues.Add(new MeasureModel
                {
                    DateTime = now,
                    Value = Convert.ToDouble(hallReply)
                });
                thermValues.Add(new MeasureModel
                {
                    DateTime = now,
                    Value = Convert.ToDouble(thermReply)
                });
                windValues.Add(new MeasureModel
                {
                    DateTime = now,
                    Value = Convert.ToDouble(windReply)
                });
            }
            else
            {
                hallValues.Add(new MeasureModel
                {
                    DateTime = now,
                    Value = 0
                });
                thermValues.Add(new MeasureModel
                {
                    DateTime = now,
                    Value = 0
                });
                windValues.Add(new MeasureModel
                {
                    DateTime = now,
                    Value = 0
                });
            }

            SetAxisLimits(now);

            //lets only use the last 30 values
            if (hallValues.Count > 60) hallValues.RemoveAt(0);
            if (thermValues.Count > 60) thermValues.RemoveAt(0);
            if (windValues.Count > 60) windValues.RemoveAt(0);

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(path, true))
            {
                file.WriteLine("");
                file.Write(GetTimeStamp(DateTime.Now));
                file.Write(", ");
                file.Write(hallReply);
                file.Write(", ");
                file.Write(thermReply);
                file.Write(", ");
                file.Write(windReply);
                file.Write("\n");
            }
        }

        private void SetAxisLimits(DateTime now)
        {
            AxisMax = now.Ticks + TimeSpan.FromSeconds(1).Ticks; // lets force the axis to be 100ms ahead
            AxisMin = now.Ticks - TimeSpan.FromSeconds(59).Ticks; //we only care about the last 8 seconds
        }

        public static String GetTimeAndDate(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }

        public static String GetTimeStamp(DateTime value)
        {
            return value.ToString("HH:mm:ss");
        }

        // ----------------------------------------------------------------------
        // Zaber Functions
        // ----------------------------------------------------------------------
        // Initializes the zaber port
        // Note: currently the objects axisX, axisY, and axisZ are not used
        // and generally don't play nicely with the rest of the code.
        public void zaberInitialize()
        {
            //Open the Zaber Actuators
            zaberPort = new ZaberAsciiPort("COM5");
            zaberPort.Open();
        }

        // Moves a device in a direction depending on the input values 
        // from the joystick
        void zaberMoveVelocity(int device, double percentToMoveDirectionOfTotal)//double moveDirection, double orthogonalDirection, bool isLeftSide)
        {
            int maxspeed = 0;
            switch (device)
            {
                case 2:
                    maxspeed = MAX_X_SPEED;
                    break;
                case 3:
                    maxspeed = MAX_Y_SPEED;
                    break;
                case 4:
                    maxspeed = MAX_Z_SPEED;
                    break;
                default:
                    Console.WriteLine("Default device passed - not moving anything");
                    return;
            }
            //Not really a percentage, as this number can be at max above or below 1,
            //depending on what your choice of joystickVelocityModulator is
            double percentOfMaxVelocityToMove = (Math.Exp(joystickVelocityModulator * Math.Abs(percentToMoveDirectionOfTotal)) - 1) / Math.Exp(joystickVelocityModulator);
            double speed = percentOfMaxVelocityToMove * maxspeed;
            int vel = (percentToMoveDirectionOfTotal > 0) ? Convert.ToInt32(speed) : Convert.ToInt32(-speed);
            string command = "";
            command = "/" + device + " 1 move vel " + Convert.ToString(vel) + "\r\n";
            zaberPort.Write(command);
            zaberPort.Read();
        }

        // Stops the zaber device indicated
        void zaberStop(int device)
        {
            if (device == 0) return;
            if (!zaberPort.IsOpen) return;
            string command = "";
            command = "/" + device + " 1 stop \r\n";
            zaberPort.Write(command);
            zaberPort.Read();
        }

        // Moves the actuators from the current position(curPos)
        // to the future position(futPos).  It avoids the programmed in no
        // fly zones after determing the move sequence.
        void zaberMoveStoredPositionOneAtATime(int[] futPos, int[] curPos)
        {
            var axisX = new ZaberAsciiAxis(zaberPort, 2, 1); // device 2, x axis
            var axisY = new ZaberAsciiAxis(zaberPort, 3, 1); // device 3, y axis
            var axisZ = new ZaberAsciiAxis(zaberPort, 4, 1); // device 4, z axis
            Console.WriteLine("In zaberMoveStoredPosition");
            int[] moveSequence = new int[3] { -1, -1, -1 };
            if (futPos.Length != 3) Console.WriteLine("In correct number of position parameters. Please input x,y,z");
            if (futPos[0] > X_MAX) futPos[0] = X_MAX;
            if (futPos[1] > Y_MAX) futPos[1] = Y_MAX;
            if (futPos[2] > Z_MAX) futPos[2] = Z_MAX;
            moveSequence = avoidNoFlyZones(futPos, curPos);  //needs a little debugging and recalibration for current setup
            for (int i =0; i < futPos.Length; i++)
            {
                switch (moveSequence[i])
                {
                    case 2: // this correlates to the x axis
                        axisX.MoveAbsolute(futPos[0]);
                        break;
                    case 3: // this correlates to the y axis
                        axisY.MoveAbsolute(futPos[1]);
                        break;
                    case 4: // this correlates to the z axis
                        axisZ.MoveAbsolute(futPos[2]);
                        break;
                    default:
                        Console.WriteLine("Something got messed up in the positioning!");
                        break;
                }
            }
        }

        void zaberMoveStoredPositionAllAtOnce(int[] futPos)
        {
            string moveX = "/2 1 move abs " + futPos[0].ToString() + "\r\n";
            string moveY = "/3 1 move abs " + futPos[1].ToString() + "\r\n";
            string moveZ = "/4 1 move abs " + futPos[2].ToString() + "\r\n";

            zaberPort.Write(moveX);
            zaberPort.Write(moveY);
            zaberPort.Write(moveZ);
            zaberPort.Read();
            zaberPort.Read();
            zaberPort.Read();
        }

        // Gets current position
        public int[] zaberGetCurrentPos()
        {
            var axisX = new ZaberAsciiAxis(zaberPort, 2, 1); // device 2, x axis
            var axisY = new ZaberAsciiAxis(zaberPort, 3, 1); // device 3, y axis
            var axisZ = new ZaberAsciiAxis(zaberPort, 4, 1); // device 4, z axis
            int[] curPos = new int[3] { axisX.GetPosition(), axisY.GetPosition(), axisZ.GetPosition() };

            return curPos;
        }

        // Converts mm to steps given the axis and mm to convert
        static int mmToSteps(string axis, double mm)
        {
            int steps = 0;
            double microStepSize = 0; //in mm

            switch (axis)
            {
                // X Axis is a X-LSQ150A Zaber Actuator
                case "x":
                    microStepSize = 0.00009921875; //in mm
                    break;
                // Y Axis is a X-LRM150A Zaber Actuator
                case "y":
                    microStepSize = 0.000047625; //in mm
                    break;
                // Z Axis is a X-LSQ150B Zaber Actuator
                case "z":
                    microStepSize = 0.00049609375; //in mm
                    break;
                default:
                    Console.WriteLine("Axis selected does not exist");
                    return 0;
            }

            steps = Convert.ToInt32(mm / microStepSize);
            return steps;
        }

        // Converts steps to mm given the axis and number of steps to convert
        static double stepsToMM(string axis, int steps)
        {
            double mm = 0;
            double microStepSize = 0;
            switch (axis)
            {
                // X Axis is a X-LSQ150A Zaber Actuator
                case "x":
                    microStepSize = 0.00009921875; //in mm
                    break;
                // Y Axis is a X-LRM150A Zaber Actuator
                case "y":
                    microStepSize = 0.000047625; //in mm
                    break;
                // Z Axis is a X-LSQ150B Zaber Actuator
                case "z":
                    microStepSize = 0.00049609375; //in mm
                    break;
                default:
                    Console.WriteLine("Axis selected does not exist");
                    return 0;
            }
            mm = microStepSize * Convert.ToDouble(steps);

            return mm;
        }

        // Structure for the NoFlyZones, units in steps
        struct NoFlyZone
        {
            public int xAxisMin, xAxisMax;
            public int yAxisMin, yAxisMax;
            public int zAxisMin, zAxisMax;
            public NoFlyZone(int xmin, int xmax, int ymin, int ymax, int zmin, int zmax)
            {
                xAxisMin = xmin;
                xAxisMax = xmax;
                yAxisMin = ymin;
                yAxisMax = ymax;
                zAxisMin = zmin;
                zAxisMax = zmax;
            }
        }

        // Define the parameters of the different NoFlyZones
        static NoFlyZone[] NoFlyZoneArray = new NoFlyZone[3]
        {
            // fill out these NoFlyZones after testing...
            // This NoFlyZone is the microtome's plastic part in steps
            new NoFlyZone(734320, X_MAX, 0, Y_MAX, 271423, Z_MAX),
            // This NoFlyZone is the microtome from approx the boat up 
            new NoFlyZone(1216976, X_MAX, 0, 1700412, 271423, Z_MAX),
            // This NoFlyZone is the block region of the microtome
            new NoFlyZone(1080153, X_MAX, 0, 499378, 0, Z_MAX)
        };

        // Check position so it doesn't run into anything
        static public bool isNoFlyZone(int[] Pos)
        {
            bool isIt = true;
            int tracker = 0;

            //for all NoFlyZones, check if position is within NoFlyZone range
            foreach (NoFlyZone myNoFlyZone in NoFlyZoneArray)
                tracker += Pos[0] > myNoFlyZone.xAxisMin ? (Pos[0] < myNoFlyZone.xAxisMax ?
                    (Pos[1] > myNoFlyZone.yAxisMin ? (Pos[1] < myNoFlyZone.yAxisMax ?
                    (Pos[2] > myNoFlyZone.zAxisMin ? (Pos[2] < myNoFlyZone.zAxisMax ?
                    1 : 0) : 0) : 0) : 0) : 0) : 0;

            if (tracker == 0) isIt = false;
            if (isIt == true) Console.Beep();

            return isIt;
        }

        // This function avoids NoFlyZones by finding an order of operations for
        // axis moves.  
        // SUPER IMPORTANT NOTE: If there is a NoFlyZone between two freezones, 
        // it may run into something!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // Make sure that tweezer tips don't "Drop" between physical objects 
        // without handling!!!!!
        public static int[] avoidNoFlyZones(int[] futPos, int[] curPos)
        {
            int[] tryPos1 = new int[] { futPos[0], curPos[1], curPos[2] };
            int[] tryPos2 = new int[] { 0, 0, 0 };
            int[] moveSequence = { -1, -1, -1 };
            // moveSequence will be initialized based on the device number
            //this means that x corresponds to device 2, y 3, and z 4

            // try xyz and xzy sequence
            if (!isNoFlyZone(tryPos1))
            {
                Array.Copy(futPos, tryPos2, 3);
                tryPos2[2] = curPos[2];
                if (!isNoFlyZone(tryPos2))
                {
                    Console.WriteLine("xyz should work");
                    moveSequence[0] = 2;
                    moveSequence[1] = 3;
                    moveSequence[2] = 4;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, 3);
                    tryPos2[1] = curPos[1];
                    if (!isNoFlyZone(tryPos2))
                    {
                        Console.WriteLine("xzy should work");
                        moveSequence[0] = 2;
                        moveSequence[1] = 4;
                        moveSequence[2] = 3;
                    }
                }
            }

            // try yxz and yzx sequence
            Array.Copy(curPos, tryPos1, 3);
            tryPos1[1] = futPos[1];
            if (!isNoFlyZone(tryPos1))
            {
                Array.Copy(futPos, tryPos2, 3);
                tryPos2[2] = curPos[2];
                if (!isNoFlyZone(tryPos2))
                {
                    Console.WriteLine("yxz should work");
                    moveSequence[0] = 3;
                    moveSequence[1] = 2;
                    moveSequence[2] = 4;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, 3);
                    tryPos2[0] = curPos[0];
                    if (!isNoFlyZone(tryPos2))
                    {
                        Console.WriteLine("yzx should work");
                        moveSequence[0] = 3;
                        moveSequence[1] = 4;
                        moveSequence[2] = 2;
                    }
                }
            }

            // try zxy and zyx sequence
            Array.Copy(curPos, tryPos1, 3);
            tryPos1[2] = futPos[2];
            if (!isNoFlyZone(tryPos1))
            {
                Array.Copy(futPos, tryPos2, 3);
                tryPos2[1] = curPos[1];
                if (!isNoFlyZone(tryPos2))
                {
                    Console.WriteLine("zxy should work");
                    moveSequence[0] = 4;
                    moveSequence[1] = 2;
                    moveSequence[2] = 3;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, 3);
                    tryPos2[0] = curPos[0];
                    if (!isNoFlyZone(tryPos2))
                    {
                        Console.WriteLine("zyx should work");
                        moveSequence[0] = 4;
                        moveSequence[1] = 3;
                        moveSequence[2] = 2;
                    }
                }
            }
            //For the future -- if needing to navigate around different things
            //add in bisection algorithm that checks to see if half-way paths 
            //can meet requirements of avoiding NoFlyZones.

            return moveSequence;
        }

        // ----------------------------------------------------------------------
        // GUI Functions
        // ----------------------------------------------------------------------
        private void buttonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (m_xbox != null)
            {
                m_xbox = null;
            }
            thorDevice.StopPolling();
            thorDevice.Disconnect();
            zaberStop(2);
            zaberStop(3);
            zaberStop(4);
            zaberPort.Close();
            Arduino.Close();
            App.Current.Shutdown();
        }

        private void buttonHome_click(object sender, RoutedEventArgs e)
        {
            thorDevice.Home(200);
        }

    }
}

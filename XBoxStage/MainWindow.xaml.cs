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
        // Files, Counters, GUI
        DispatcherTimer _gamepadTimer = new DispatcherTimer();
        DispatcherTimer _dataTimer = new DispatcherTimer();
        private byte[] pingMessage = System.Text.Encoding.ASCII.GetBytes("ping\n");
        private double _axisMax;
        private double _axisMin;
        public int visibleDataPoints = 60;
        public event PropertyChangedEventHandler PropertyChanged;
        // Files
        public string sensorFile;
        public string buttonFile;
        public string zaberFile; //for debugging mostly...
        public string resourcePathRoot = @"C:\Users\Public\XBoxStageVersions\XBoxStageRefactor\XBoxStageMaster\XBoxStage\Resources\";
        public string outputPathRoot = @"C:\Users\Public\SerialSectionSessions\";
        // FSM 
        public int sectionsProcessed;
        public int loopPickUpCount;
        public int loopDropOffCount;
        // Stick Stuff -- make this into a data type later for sticks/consumable sticks
        public int numberOfCols = 8; // this is specifically made for sticks as consumables
        public int numberOfSticks = 4; //this is the number of sticks on the casette
        public double xGridDisplacement = 5.3; //these are the relative positions of the grids
        public double yGridDisplacement = 8.6; // on the consumable sticks
        public double xLoopDisplacement = 3.6; // these are the relative positions of the loops
        public double yLoopDisplacement = 3.6; // on the mats, in mm
        public int totalNumberOfLoops = 64;
        public bool sticksAsConsumables = true;

        // Thorlabs Actuator (serial number provided is specific to my device)
        public KCubeDCServo thorDevice;
        public string serialNo = "27000117";
        public int thorPollRate = 20;
        public decimal thorDisplacement = 1m;

        // Arduino and sensor output stuff
        public SerialPort Arduino = new SerialPort();

        // Zaber
        static public int X_MAX = 200000;
        static public int Y_MAX = 400000;
        static public int Z_MAX = 305381;
        public const int x_Device = 2;
        public const int y_Device = 3;
        public const int z_Device = 4;
        int MAX_X_SPEED = mmToSteps("x", 20.0);
        int MAX_Y_SPEED = mmToSteps("y", 20.0);
        int MAX_Z_SPEED = mmToSteps("z", 10.0);
        public ZaberAsciiPort zaberPort;
        public int[] curPos = new int[3];
        public int[] XButtonPosition = new int[3];
        public int[] YButtonPosition = new int[3];
        public int[] BButtonPosition = new int[3];
        public double joystickVelocityModulator = 1.05;
        
        // XBox
        private SharpDX.XInput.State m_xboxState;
        private SharpDX.XInput.State m_xboxStateLast;
        private SharpDX.XInput.Controller m_xbox;
        public int XBOX_MAX_RANGE = 32767;
        public int rDeadZone = 6000; //Gamepad.RightThumbDeadZone; 
        public int lDeadZone = 3000; //Gamepad.LeftThumbDeadZone; 
        public bool LeftLastStateDeadZone = false;
        public bool LeftCurrentStateDeadZone = false;
        public bool RightLastStateDeadZone = false;
        public bool RightCurrentStateDeadZone = false;
        public bool SimultaneousBackandBCur = false;
        public bool SimultaneousBackandBLast = false;
        public bool SimultaneousBackandXCur = false;
        public bool SimultaneousBackandXLast = false;
        public bool SimultaneousBackandYCur = false;
        public bool SimultaneousBackandYLast = false;
        public bool SimultaneousDownDandBLast = false;
        public bool SimultaneousDownDandBCur = false;
        public bool SimultaneousDownDandXLast = false;
        public bool SimultaneousDownDandXCur = false;
        static public int POLL_RATE = 30; //in ms NOTE: XBox is limited to 30ms minimum
        //For the Classic Buttons
        public event EventHandler OnXBoxGamepadButtonPressA;
        public event EventHandler OnXBoxGamepadButtonPressB;
        public event EventHandler OnXBoxGamepadButtonPressX;
        public event EventHandler OnXBoxGamepadButtonPressY;
        public event EventHandler MoveToLastDropOffPosition;
        public event EventHandler MoveToLastPickUpPosition; 


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

        // ----------------------------------------------------------------------
        // Computer Actions
        // ----------------------------------------------------------------------
        // Play a sound located in resource file where soundFileName = "mysound.wav"
        void playSound(string soundFileName)
        {
            SoundPlayer sound = new SoundPlayer();
            sound.SoundLocation = resourcePathRoot + soundFileName;
            sound.Play();
            sound.Dispose();
            return;
        }

        //Record one of the position button presses
        void recordButtonPress(string button)
        {
            int[] buttonPosition = new int[3];
            string message = "";
            switch (button)
            {
                case "B":
                    Array.Copy(BButtonPosition, buttonPosition, 3);
                    break;
                case "X":
                    Array.Copy(XButtonPosition, buttonPosition, 3);
                    break;
                case "Y":
                    Array.Copy(YButtonPosition, buttonPosition, 3);
                    break;
                default:
                    Console.WriteLine("Button Pressed not registered");
                    return;
            }
            message = button + ", ";
            Console.WriteLine(message);
            curPos = zaberGetCurrentPos();
            Console.WriteLine("curPos:({0}, {1}, {2})", curPos[0], curPos[1], curPos[2]);
            Console.WriteLine("futPos: ({0}, {1}, {2})", buttonPosition[0], buttonPosition[1], buttonPosition[2]);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(buttonFile, true))
            {
                file.WriteLine("");
                file.Write(GetTimeStamp(DateTime.Now));
                file.Write(", {0}, {1}, {2}, {3}, ", button, buttonPosition[0], buttonPosition[1], buttonPosition[2]);
                file.Write("{0}, {1}, {2}", curPos[0], curPos[1], curPos[2]);
                file.Write("\n");
            }
        }

        // ----------------------------------------------------------------------
        // GUI
        // ----------------------------------------------------------------------
        // Events to execute on program closing 
        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            thorDevice.StopPolling();
            thorDevice.Disconnect();
            zaberStop(x_Device);
            zaberStop(y_Device);
            zaberStop(z_Device);
            zaberPort.Close();
            Arduino.Close();
            m_xbox = null;
            
        }

        // Events to execute on program loading
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //playSound("trumpets.wav");
            m_xbox = new Controller(UserIndex.One);
            if (m_xbox.IsConnected) return;
            System.Windows.MessageBox.Show("Controller is not connected");
            App.Current.Shutdown();
        }

        private void buttonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (m_xbox != null)
            {
                m_xbox = null;
            }
            thorDevice.StopPolling();
            thorDevice.Disconnect();
            zaberStop(x_Device);
            zaberStop(y_Device);
            zaberStop(z_Device);
            zaberPort.Close();
            Arduino.Close();
            App.Current.Shutdown();
        }

        // Events to execute if user clicks loop placement checkbox
        private void bInitialization_Click(object sender, RoutedEventArgs e)
        {
            if (sender as System.Windows.Forms.CheckBox == null)
            {
                return;
            }
            if ((sender as System.Windows.Forms.CheckBox).Checked)
            {
                //(sender as System.Windows.Forms.CheckBox).Checked = false;
            }
            else
            {
                (sender as System.Windows.Forms.CheckBox).Checked = true;
            }
        }

        // Events to execute if user clicks loop dropoff checkbox
        private void xInitialization_Click(object sender, RoutedEventArgs e)
        {
            if (sender as System.Windows.Forms.CheckBox == null)
            {
                return;
            }
            if ((sender as System.Windows.Forms.CheckBox).Checked)
            {
                (sender as System.Windows.Forms.CheckBox).Checked = false;
            }
            else
            {
                (sender as System.Windows.Forms.CheckBox).Checked = true;
            }
        }

        // Events to execute if user clicks section pickup checkbox
        private void yInitialization_Click(object sender, RoutedEventArgs e)
        {
            if (sender as System.Windows.Forms.CheckBox == null)
            {
                return;
            }
            if ((sender as System.Windows.Forms.CheckBox).Checked)
            {
                (sender as System.Windows.Forms.CheckBox).Checked = false;
            }
            else
            {
                (sender as System.Windows.Forms.CheckBox).Checked = true;
            }
        }

        private void buttonHome_click(object sender, RoutedEventArgs e)
        {
            //thorDevice.Home(200);
        }

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

            //Start thorDevice polling
            thorDevice.StartPolling(thorPollRate);

            //Initialize counters for pick up and drop off
            loopPickUpCount = 0;
            loopDropOffCount = 0;

            // This guy does the gamepad polling every however many ms you want it to. 
            // The higher the sampling rate, the more likely it'll bork. YOU'VE BEEN WARNED!!
            _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(POLL_RATE) };
            _gamepadTimer.Tick += _timer_Tick;
            _gamepadTimer.Start();

            // Initialize filenames and data structures for sensor data
            string timeAndDate = GetTimeAndDate(DateTime.Now);
            sensorFile = outputPathRoot + "sensorOutput" + timeAndDate + ".txt"; 
            buttonFile = outputPathRoot + "buttonOutput" + timeAndDate + ".txt";
            zaberFile = outputPathRoot + "zaberOutput" + timeAndDate + ".txt";

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(buttonFile, true))
            {
                file.Write("TimeStamp, ButtonPressed, XfuturePosition, YfuturePosition, ZfuturePosition, XcurrentPosition, YcurrentPosition, ZcurrentPosition");
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(sensorFile, true))
            {
                file.Write("TimeStamp, HallEffectReading(bytes), TemperatureReading(C), WindReading(bytes)");
            }

            String timeStamp = GetTimeStamp(DateTime.Now);
            Console.WriteLine("The time stamp is: {0}", timeStamp);

            var hallMapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y
            var thermMapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)
                .Y(model => model.Value);        
            var windMapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)
                .Y(model => model.Value);        

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
            _dataTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _dataTimer.Tick += _dataTimerOnTick;
            _dataTimer.Start();
            DataContext = this;

            // Do something for this button being pressed
            // Classic Buttons
            OnXBoxGamepadButtonPressA += (s, e) =>  //Button A is the Emergency Stop
            {
                zaberStop(x_Device);
                zaberStop(y_Device);
                zaberStop(z_Device);
                thorDevice.StopImmediate();
                playSound("buzzer.wav");
            };
            // Do something for this button being held

            OnXBoxGamepadButtonPressB += (s, e) => //Button B places the loops
            {
                recordButtonPress("B");
                playSound("100Hz2s.wav");
                zaberMoveNextDropOffPoint();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(buttonFile, true))
                {
                    file.WriteLine("");
                    file.Write(GetTimeStamp(DateTime.Now));
                    file.Write(", number of loops placed on grids: ", loopDropOffCount);
                    file.Write("\n");
                }
                loopDropOffCount++;
            };

            OnXBoxGamepadButtonPressX += (s, e) => //Button X picks up the loops
            {
                recordButtonPress("X");
                playSound("200Hz2s.wav");
                zaberMoveNextPickUpPoint();
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(buttonFile, true))
                {
                    file.WriteLine("");
                    file.Write(GetTimeStamp(DateTime.Now));
                    file.Write(", number of loops picked up: ", loopPickUpCount);
                    file.Write("\n");
                }
                loopPickUpCount++;
            };

            OnXBoxGamepadButtonPressY += (s, e) => //Button Y picks up the sections
            {
                recordButtonPress("Y");
                playSound("300Hz2s.wav");
                zaberMoveStoredPositionAllAtOnce(YButtonPosition);
            };

            MoveToLastDropOffPosition += (s, e) => //Set back loopDropOff counter and move to previous loop position
            {
                loopDropOffCount -= 2;
                zaberMoveNextDropOffPoint();
            };

            MoveToLastPickUpPosition += (s, e) => //Set back loopPickUp counter and move to previous loop position
            {
                loopPickUpCount -= 2;
                zaberMoveNextPickUpPoint();
            };
        }

        private void SectionCounter_TextChanged(object sender, TextChangedEventArgs e)
        {
            Int32.TryParse(sectionCountTxt.Text, out sectionsProcessed);
        }

        // ----------------------------------------------------------------------
        // XBox Controller Functions
        // ----------------------------------------------------------------------
        // Initialize the XBox controller
        public void xboxInitialize()
        {
            // Attempt to connect ot any port
            m_xbox = new SharpDX.XInput.Controller(UserIndex.Any);
            // If it cannot be connected, shut down.
            if (!m_xbox.IsConnected)
            {
                Console.WriteLine("XBox Controller Could not be connected. Goodbye!");
                System.Threading.Thread.Sleep(2000);
                App.Current.Shutdown();
            }
        }

        // Return true if button is pressed
        private bool buttonPressed(GamepadButtonFlags button)
        {
            return !m_xboxStateLast.Gamepad.Buttons.HasFlag(button) && m_xboxState.Gamepad.Buttons.HasFlag(button);
        }

        // Return true if button is released
        private bool buttonReleased(GamepadButtonFlags button)
        {
            return m_xboxStateLast.Gamepad.Buttons.HasFlag(button) && !m_xboxState.Gamepad.Buttons.HasFlag(button);
        }

        private bool simultaneousButtons(GamepadButtonFlags button1, GamepadButtonFlags button2)
        {
            return m_xboxState.Gamepad.Buttons.HasFlag(button1) && m_xboxState.Gamepad.Buttons.HasFlag(button2);
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
                    xDevice = x_Device; //corresponds to zaber x-axis
                    yDevice = y_Device; //corresponds to zaber y-axis
                    break;
                case "right":
                    currentStateDeadZone = RightCurrentStateDeadZone;
                    lastStateDeadZone = RightLastStateDeadZone;
                    deadzone = rDeadZone;
                    isLeftSide = false;
                    xDevice = 0; //this will make it default in zaber move, just returning
                    yDevice = z_Device; //corresponds to zaber z-axis
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
            if ((m_xbox == null) || !m_xbox.IsConnected) return;
            // Update statuses 
            m_xboxStateLast = m_xboxState;
            m_xboxState = m_xbox.GetState();
            LeftLastStateDeadZone = LeftCurrentStateDeadZone;
            RightLastStateDeadZone = RightCurrentStateDeadZone;
            //Update the deadzone status and check for joystick controls
            // Do the Right and Left Thumb X, Y joysticks
            double RX = m_xboxState.Gamepad.RightThumbX;
            double RY = m_xboxState.Gamepad.RightThumbY;
            double LX = m_xboxState.Gamepad.LeftThumbX;
            double LY = m_xboxState.Gamepad.LeftThumbY;
            xboxJoystick("left", LX, LY);
            xboxJoystick("right", RX, RY);

            //Position Save Combo States Accounting
            SimultaneousBackandBLast = SimultaneousBackandBCur;
            SimultaneousBackandXLast = SimultaneousBackandXCur;
            SimultaneousBackandYLast = SimultaneousBackandYCur;
            SimultaneousBackandBCur = simultaneousButtons(GamepadButtonFlags.Back, GamepadButtonFlags.B);
            SimultaneousBackandXCur = simultaneousButtons(GamepadButtonFlags.Back, GamepadButtonFlags.X);
            SimultaneousBackandYCur = simultaneousButtons(GamepadButtonFlags.Back, GamepadButtonFlags.Y);
            //Decrementing Counter States Accounting
            SimultaneousDownDandBLast = SimultaneousDownDandBCur;
            SimultaneousDownDandBCur = simultaneousButtons(GamepadButtonFlags.DPadDown, GamepadButtonFlags.B);
            SimultaneousDownDandXLast = SimultaneousDownDandXCur;
            SimultaneousDownDandXCur = simultaneousButtons(GamepadButtonFlags.DPadDown, GamepadButtonFlags.X);

            // Event handlers for buttons being pushed
            // Classic Buttons
            if (buttonPressed(GamepadButtonFlags.A)) OnXBoxGamepadButtonPressA.Invoke(this, null);
            if ((buttonPressed(GamepadButtonFlags.B) && !m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back)) && (LeftCurrentStateDeadZone && RightCurrentStateDeadZone)) OnXBoxGamepadButtonPressB.Invoke(this, null);
            if ((buttonPressed(GamepadButtonFlags.X) && !m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back)) && (LeftCurrentStateDeadZone && RightCurrentStateDeadZone)) OnXBoxGamepadButtonPressX.Invoke(this, null);
            if ((buttonPressed(GamepadButtonFlags.Y) && !m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back)) && (LeftCurrentStateDeadZone && RightCurrentStateDeadZone)) OnXBoxGamepadButtonPressY.Invoke(this, null);

            // Left/Right Shoulder Thorlabs device control
            if (buttonPressed(GamepadButtonFlags.RightShoulder) && !simultaneousButtons(GamepadButtonFlags.RightShoulder, GamepadButtonFlags.LeftShoulder))
            {
                thorMove(thorDevice, MotorDirection.Forward);
            }
            if (buttonPressed(GamepadButtonFlags.LeftShoulder) && !simultaneousButtons(GamepadButtonFlags.LeftShoulder, GamepadButtonFlags.RightShoulder))
            {
                thorMove(thorDevice, MotorDirection.Backward);
            }

            // Combos to save positions -- make it have last state too so that it updates once
            if (SimultaneousBackandBCur && !SimultaneousBackandBLast)
            {
                BButtonPosition = zaberGetCurrentPos();
                loopDropOffCount = 0;
                bInitialization.IsChecked = true;
            }
            if (SimultaneousBackandXCur && !SimultaneousBackandXLast)
            {
                XButtonPosition = zaberGetCurrentPos();
                loopPickUpCount = 0;
                xInitialization.IsChecked = true;
            }
            if (SimultaneousBackandYCur && !SimultaneousBackandYLast)
            {
                YButtonPosition = zaberGetCurrentPos();
                yInitialization.IsChecked = true;
            }

            // Combos to decrement button counters
            if (SimultaneousDownDandBCur && !SimultaneousDownDandBLast)
            {
                MoveToLastDropOffPosition.Invoke(this, null);
            }
            if (SimultaneousDownDandXCur && !SimultaneousDownDandXLast)
            {
                MoveToLastPickUpPosition.Invoke(this, null);
            }

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

        public void thorMove(IGenericAdvancedMotor device, MotorDirection direction)
        {
            
            try
            {
                //device.MoveContinuous(direction);
                device.MoveRelative(direction, thorDisplacement, 100);
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

        private void _dataTimerOnTick(object sender, EventArgs eventArgs)
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
            if (hallValues.Count > visibleDataPoints) hallValues.RemoveAt(0);
            if (thermValues.Count > visibleDataPoints) thermValues.RemoveAt(0);
            if (windValues.Count > visibleDataPoints) windValues.RemoveAt(0);

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(sensorFile, true))
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
                case x_Device:
                    maxspeed = MAX_X_SPEED;
                    break;
                case y_Device:
                    maxspeed = MAX_Y_SPEED;
                    break;
                case z_Device:
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
            var reply = zaberPort.Read(); //reply and below is for debugging
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(zaberFile, true))
            {
                file.WriteLine("");
                file.Write(GetTimeStamp(DateTime.Now));
                file.WriteLine("In Function zaberMoveVelocity");
                file.WriteLine("Command sent: ");
                file.WriteLine(command); //record commands
                file.WriteLine("Reply received: "); //record replies
                file.WriteLine(reply.ToString());
                file.Write("\n");
            }
            if (!(reply.ToString().Contains("--") || reply.ToString().Contains("NI"))) zaberClearWarnings(device);
        }

        // Stops the zaber device indicated
        void zaberStop(int device)
        {
            if (device == 0) return;
            if (!zaberPort.IsOpen) return;
            string command = "";
            command = "/" + device + " 1 stop \r\n";
            zaberPort.Write(command);
            var reply = zaberPort.Read(); //reply and below is for debugging
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(zaberFile, true))
            {
                file.WriteLine("");
                file.Write(GetTimeStamp(DateTime.Now));
                file.WriteLine("In Function zaberStop");
                file.WriteLine("Command sent: ");
                file.WriteLine(command); //record commands
                file.WriteLine("Reply received: "); //record replies
                file.WriteLine(reply.ToString());
                file.Write("\n");
            }
            if (!(reply.ToString().Contains("--") || reply.ToString().Contains("NI"))) zaberClearWarnings(device);
        }

        // Moves the actuators from the current position(curPos)
        // to the future position(futPos).  It avoids the programmed in no
        // fly zones after determing the move sequence.
        void zaberMoveStoredPositionOneAtATime(int[] futPos, int[] curPos)
        {
            var axisX = new ZaberAsciiAxis(zaberPort, x_Device, 1); 
            var axisY = new ZaberAsciiAxis(zaberPort, y_Device, 1); 
            var axisZ = new ZaberAsciiAxis(zaberPort, z_Device, 1);
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
                    case x_Device: // this correlates to the x axis
                        axisX.MoveAbsolute(futPos[0]);
                        break;
                    case y_Device: // this correlates to the y axis
                        axisY.MoveAbsolute(futPos[1]);
                        break;
                    case z_Device: // this correlates to the z axis
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
            string moveX = "/" + x_Device + " 1 move abs " + futPos[0].ToString() + "\r\n";
            string moveY = "/" + y_Device + " 1 move abs " + futPos[1].ToString() + "\r\n";
            string moveZ = "/" + z_Device + " 1 move abs " + futPos[2].ToString() + "\r\n";

            zaberPort.Write(moveX);
            zaberPort.Write(moveY);
            zaberPort.Write(moveZ);
            var reply1 = zaberPort.Read(); //reply1-reply3 and below is for debugging
            var reply2 = zaberPort.Read(); //still need to include '.Read()' for proper 
            var reply3 = zaberPort.Read(); //functioning
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(zaberFile, true))
            {
                file.WriteLine("");
                file.Write(GetTimeStamp(DateTime.Now));
                file.WriteLine("In Function zaberMoveStoredPositionAllAtOnce");
                file.WriteLine("Commands sent: ");
                file.WriteLine(moveX); //record commands
                file.WriteLine(moveY); 
                file.WriteLine(moveZ);
                file.WriteLine("Replies received: "); //record replies
                file.WriteLine(reply1.ToString());
                file.WriteLine(reply2.ToString());
                file.WriteLine(reply3.ToString());
                file.Write("\n");
            }
            if (!(reply1.ToString().Contains("--") || reply1.ToString().Contains("NI"))) zaberClearWarnings(x_Device);
            if (!(reply2.ToString().Contains("--") || reply2.ToString().Contains("NI"))) zaberClearWarnings(y_Device);
            if (!(reply3.ToString().Contains("--") || reply3.ToString().Contains("NI"))) zaberClearWarnings(z_Device);
        }

        // Calculates next move for loop drop off and executes it
        void zaberMoveNextDropOffPoint()
        {
            int[] futPos = new int[3];
            int xDisplacementFromOrigin = 0;
            int yDisplacementFromOrigin = 0;
            int colPosition, colNumber;
            colPosition = loopDropOffCount % numberOfSticks;
            colNumber = loopDropOffCount / numberOfSticks;
            int virtColNum = colNumber;
            if (colNumber >= numberOfCols)
            {
                xDisplacementFromOrigin -= mmToSteps("x", 2.65);
                yDisplacementFromOrigin += mmToSteps("y", 2.0);
                virtColNum -= numberOfCols;
            }
            xDisplacementFromOrigin += virtColNum * mmToSteps("x", xGridDisplacement);
            yDisplacementFromOrigin += colPosition * mmToSteps("y", yGridDisplacement);
            futPos[0] = xDisplacementFromOrigin + BButtonPosition[0];
            futPos[1] = yDisplacementFromOrigin + BButtonPosition[1];
            futPos[2] = BButtonPosition[2];
            zaberMoveStoredPositionAllAtOnce(futPos);
            if (loopDropOffCount >= totalNumberOfLoops) loopDropOffCount = 0; //reset for next cassette
        }

        // Calculates next move for loop pick up and executes it
        void zaberMoveNextPickUpPoint()
        {
            int[] futPos = new int[3];
            int xDisplacementFromOrigin = 0;
            int yDisplacementFromOrigin = 0;
            int colPosition, colNumber;
            colNumber = loopPickUpCount / 10;
            colPosition = loopPickUpCount % 10;

            // do the col of 10 that has staggered downward position w.r.t. top leftmost element
            if ((loopPickUpCount / 10) % 2 != 0)
            {
                yDisplacementFromOrigin += mmToSteps("y", (yLoopDisplacement/2));
            }
            xDisplacementFromOrigin += colNumber * mmToSteps("x", xLoopDisplacement);
            yDisplacementFromOrigin += colPosition * mmToSteps("y", yLoopDisplacement);
            futPos[0] = xDisplacementFromOrigin + XButtonPosition[0];
            futPos[1] = yDisplacementFromOrigin + XButtonPosition[1];
            futPos[2] = XButtonPosition[2];
            zaberMoveStoredPositionAllAtOnce(futPos);
        }

        // Gets current position
        public int[] zaberGetCurrentPos()
        {
            var axisX = new ZaberAsciiAxis(zaberPort, x_Device, 1); 
            var axisY = new ZaberAsciiAxis(zaberPort, y_Device, 1); 
            var axisZ = new ZaberAsciiAxis(zaberPort, z_Device, 1); 
            int[] curPos = new int[3] { axisX.GetPosition(), axisY.GetPosition(), axisZ.GetPosition() };

            return curPos;
        }

        // Clears warning messages
        public void zaberClearWarnings(int device)
        {
            string clearWarnings = "/" + device + " warnings clear\r\n"; //add "clear" at the end?
            zaberPort.Write(clearWarnings);
            var reply = zaberPort.Read();
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(zaberFile, true))
            {
                file.WriteLine("");
                file.Write(GetTimeStamp(DateTime.Now));
                file.WriteLine("In Function zaberClearWarnings");
                file.WriteLine("Command sent: ");
                file.WriteLine(clearWarnings); //record commands
                file.WriteLine("Reply received: "); //record replies
                file.WriteLine(reply.ToString());
                file.Write("\n");
            }
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
                    microStepSize = 0.000248046875; //in mm
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
                    moveSequence[0] = x_Device;
                    moveSequence[1] = y_Device;
                    moveSequence[2] = z_Device;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, 3);
                    tryPos2[1] = curPos[1];
                    if (!isNoFlyZone(tryPos2))
                    {
                        Console.WriteLine("xzy should work");
                        moveSequence[0] = x_Device;
                        moveSequence[1] = z_Device;
                        moveSequence[2] = y_Device;
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
                    moveSequence[0] = y_Device;
                    moveSequence[1] = x_Device;
                    moveSequence[2] = z_Device;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, 3);
                    tryPos2[0] = curPos[0];
                    if (!isNoFlyZone(tryPos2))
                    {
                        Console.WriteLine("yzx should work");
                        moveSequence[0] = y_Device;
                        moveSequence[1] = z_Device;
                        moveSequence[2] = x_Device;
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
                    moveSequence[0] = z_Device;
                    moveSequence[1] = x_Device;
                    moveSequence[2] = y_Device;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, 3);
                    tryPos2[0] = curPos[0];
                    if (!isNoFlyZone(tryPos2))
                    {
                        Console.WriteLine("zyx should work");
                        moveSequence[0] = z_Device;
                        moveSequence[1] = y_Device;
                        moveSequence[2] = x_Device;
                    }
                }
            }
            //For the future -- if needing to navigate around different things
            //add in bisection algorithm that checks to see if half-way paths 
            //can meet requirements of avoiding NoFlyZones.

            return moveSequence;
        }

    }
}

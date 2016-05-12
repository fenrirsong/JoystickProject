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
using System.IO.Ports;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime;

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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        DispatcherTimer _timer = new DispatcherTimer();
        private string _leftAxis;
        private string _rightAxis;
        private string _buttons;
        static public int POLL_RATE = 30; //in ms NOTE: XBox is limited to 30ms min
        // GUI
        //public TextBoxStreamWriter _writer = null;

        // XBox
        private SharpDX.XInput.State m_xboxState;
        private SharpDX.XInput.State m_xboxStateLast;
        private SharpDX.XInput.Controller m_xbox = new Controller(UserIndex.One);
        public int XBOX_MAX_RANGE = 32767;

        private byte[] pingMessage = System.Text.Encoding.ASCII.GetBytes("ping\n");

        // Zaber
        static public int X_MAX = 1526940;
        static public int Y_MAX = 3149606;
        static public int Z_MAX = 305381;
        int MAX_X_SPEED = mmToSteps("x", 20.0);
        int MAX_Y_SPEED = mmToSteps("y", 20.0);
        int MAX_Z_SPEED = mmToSteps("z", 10.0);
        public ZaberAsciiAxis axisX; //don't really need these....
        public ZaberAsciiAxis axisY;
        public ZaberAsciiAxis axisZ;
        public ZaberAsciiPort zaberPort;
        int[] curPos = new int[3];
        int[] futPos = new int[3]; //this guy is un-used at the moment...
        public int[] XButtonPosition = new int[3];
        public int[] YButtonPosition = new int[3];
        public int[] BButtonPosition = new int[3];
        public double joystickVelocityModulator = 1.2;
        public int rDeadZone = Gamepad.RightThumbDeadZone; //Might not work here...
        public int lDeadZone = Gamepad.LeftThumbDeadZone;
        public bool LeftLastStateDeadZone = false;
        public bool LeftCurrentStateDeadZone = false;
        public bool RightLastStateDeadZone = false;
        public bool RightCurrentStateDeadZone = false;

        // Thorlabs Actuator -- may need to initialize the necessary vars here
        // position and velocity are just guesses at suitable numbers -- needs
        //to be played with in order to get a good value, but must be in decimal format
        // This is our guy's serial number -- will not be the same for others
        public KCubeDCServo thorDevice;
        public decimal thorPosition = 17m;
        public string serialNo = "27000117";
        
        // Arduino
        public SerialPort Arduino = new SerialPort();

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
        // Properties which support binding in the UI
        // ----------------------------------------------------------------------
        private bool _stageInitialized = false;
        public bool StageInitialized        {
            get
            {
                return _stageInitialized;
            }
            set
            {
                _stageInitialized = value;
                EnabledBrush = new SolidColorBrush(_stageInitialized ? Colors.LightBlue : Colors.LightGray);
                OnPropertyChanged();
                OnPropertyChanged("EnabledBrush");

            }
        }

        public string LeftAxis
        {
            get
            {
                return _leftAxis;
            }
            set
            {
                if (value == _leftAxis)
                {
                    //For Debugging
                    //Console.WriteLine("No change in Left Axis");
                    return;
                }
                //For Debugging
                /*else
                {
                    Console.WriteLine("New Left Axis value: \"{0}\"", _leftAxis);
                }*/
                _leftAxis = value;
                OnPropertyChanged("No change in Left Axis");
            }
        }

        public string RightAxis
        {
            get
            {
                return _rightAxis;
            }
            set
            {
                if (value == _rightAxis)
                {
                    //Console.WriteLine("No change in Right Axis");
                    return;
                }
                /*else
                {
                    Console.WriteLine("New Right Axis value: \"{0}\"", _rightAxis);
                }*/
                _rightAxis = value;
                OnPropertyChanged("No change in Right Axis");
            }
        }

        public string Buttons
        {
            get
            {
                return _buttons;
            }
            set
            {
                if (value == _buttons)
                {
                    //Console.WriteLine("NoNewButtons");
                    return;
                }
                /*else
                {
                    Console.WriteLine("New _buttons Values: \"{0}\"", _buttons);
                }*/
                _buttons = value;
                OnPropertyChanged("NoNewButtons");
            }
        }

        public SolidColorBrush EnabledBrush
        {
            get; set;
        }

        // ----------------------------------------------------------------------
        // Property notification boilerplate
        // ----------------------------------------------------------------------
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        // The _time_Tick function happens every x ms (set in MainWindow())
        // and polls the gamepad each time
        void _timer_Tick(object sender, EventArgs e)
        {
            PollGamepad();
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            m_xbox = null;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
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
            //InitializeComponent();

            // Initialize GUI textbox
            //_writer = new TextBoxStreamWriter(txtConsole);
            //Console.SetOut(_writer);
            Console.WriteLine("Now redirecting output to the text box");

            // Open the Arduino
            Arduino.PortName = "COM6";
            Arduino.BaudRate = 9600;
            Arduino.Open();

            //Open the Zaber Actuators -- need to figure out the specifics of
            // which port maps to which component.
            zaberPort = new ZaberAsciiPort("COM5");
            zaberPort.Open();
            axisX = new ZaberAsciiAxis(zaberPort, 2, 1); // device 2, x axis
            axisY = new ZaberAsciiAxis(zaberPort, 3, 1); // device 3, y axis
            axisZ = new ZaberAsciiAxis(zaberPort, 4, 1); // device 4, z axis
            //*********bool isItNoFlyZone = false; *****//


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
            int debicesCount = serialNumbers.Count();
            string debices = string.Join(",", serialNumbers.ToArray());
            Console.WriteLine("Number of Debices found \"{0}\"", debicesCount);
            Console.WriteLine("Debices found \"{0}\"", debices);
            if (debices == "") Console.WriteLine("none of that device shit found!!");
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
                    if (thorDevice.IsSettingsInitialized()) Console.WriteLine("Shit shoulda initialized");
                }
                catch (Exception)
                {
                    Console.WriteLine("Settings failed to initialize");
                }
            }
            
            thorDevice.StartPolling(250);


            // call GetMotorConfiguration on the device to initialize the DeviceUnitConverter object required for real world unit parameters  
            MotorConfiguration motorSettings = thorDevice.GetMotorConfiguration(serialNo);
            //might be useful in the future, but, currently not that useful, and not setup
            //for a DC servo, which is what we have...
            //BrushlessMotorSettings currentDeviceSettings = thorDevice.MotorDeviceSettings as BrushlessMotorSettings; 

            // display info about device     
            DeviceInfo deviceInfo = thorDevice.GetDeviceInfo();
            Console.WriteLine("Device {0} = {1}", deviceInfo.SerialNumber, deviceInfo.Name);
            

            // This guy does the gamepad polling every however many ms you want it to. 
            // The higher the sampling rate, the more likely it'll bork. YOU'VE BEEN WARNED!!
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(POLL_RATE) };
            _timer.Tick += _timer_Tick;
            _timer.Start();

            // Make sure that XBox stuff is open, assign it deadzones
            
            // If the controller was not assigned to player One during initialization
            // attempt to connect to a player port
            if (!m_xbox.IsConnected)
            {
                Console.WriteLine("XBox Controller Not Connected to One. Trying other connections");
                m_xbox = new SharpDX.XInput.Controller(UserIndex.Any);
                if (!m_xbox.IsConnected)
                {
                    Console.WriteLine("XBox Controller Could not be connected. Goodbye!");
                    System.Threading.Thread.Sleep(2000);
                    App.Current.Shutdown();
                }
            }

            Console.WriteLine("Inside of MainWindow()");

            // Do something for this button being pressed
            // Classic Buttons
            OnXBoxGamepadButtonPressA += (s, e) =>
            {
                Console.WriteLine("ButtonA -- stop all");
                zaberStop(2);
                zaberStop(3);
                zaberStop(4);
                thorDevice.StopImmediate();
            };
            // Do something for this button being held
            OnXBoxGamepadButtonPressAOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonAOneShot");
            };

            OnXBoxGamepadButtonPressB += (s, e) =>
            {
                Console.WriteLine("ButtonB Move to designated B position");
                zaberSetCurrentPosTo(curPos);
                zaberMoveStoredPosition(BButtonPosition, curPos);
            };
            OnXBoxGamepadButtonPressBOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonBOneShot");
            };

            OnXBoxGamepadButtonPressX += (s, e) =>
            {
                Console.WriteLine("ButtonX move to designated X position");
                zaberSetCurrentPosTo(curPos);
                zaberMoveStoredPosition(XButtonPosition, curPos);
            };
            OnXBoxGamepadButtonPressXOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonXOneShot");
            };

            OnXBoxGamepadButtonPressY += (s, e) =>
            {
                Console.WriteLine("ButtonY move to designated Y position");
                zaberSetCurrentPosTo(curPos);
                zaberMoveStoredPosition(YButtonPosition, curPos);
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

            //make this guy control the air
            OnXBoxGamepadButtonPressDDown += (s, e) =>
            {
                //Console.WriteLine("ButtonDDown");
                if (Arduino.IsOpen)
                {
                    Arduino.Write("A");
                }
            };
            OnXBoxGamepadButtonPressDDownOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonDDownOneShot");
            };

            OnXBoxGamepadButtonPressDLeft += (s, e) =>
            {
                //Console.WriteLine("ButtonDLeft");
                thorMove(thorDevice, MotorDirection.Backward);
            };
            OnXBoxGamepadButtonPressDLeftOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonDLeftOneShot");
            };

            OnXBoxGamepadButtonPressDRight += (s, e) =>
            {
                //Console.WriteLine("ButtonDRight");
                thorMove(thorDevice, MotorDirection.Forward);
            };
            OnXBoxGamepadButtonPressDRightOneShot += (s, e) =>
            {
                //Console.WriteLine("ButtonDRightOneShot");
            };

            // Shoulder and ThumbsIn Buttons
            OnXBoxGamepadButtonPressShoulderRight += (s, e) =>
            {
                Console.WriteLine("ButtonShoulderRight");
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

            StageInitialized = false;
        }


        // ----------------------------------------------------------------------
        // XBox
        // ----------------------------------------------------------------------

        // return true if the button state just transitioned from 0 to 1
        // Gives more control to the button
        private bool ButtonOneShot (GamepadButtonFlags button)
        {
            return !m_xboxStateLast.Gamepad.Buttons.HasFlag(button) && m_xboxState.Gamepad.Buttons.HasFlag(button);
        }

        // return true if the button is pushed 
        private bool ButtonPushed(GamepadButtonFlags button)
        {
            return m_xboxState.Gamepad.Buttons.HasFlag(button);
        }

        // Main XBox processing
        private void PollGamepad()
        {
            var thorDeviceAcc = thorDevice.AdvancedMotorLimits.AccelerationMaximum;
            var thorDeviceVel = 1m;
            var thorVelParams = thorDevice.GetVelocityParams();
            thorVelParams.Acceleration = thorDeviceAcc;
            thorVelParams.MaxVelocity = thorDeviceVel;
            thorDevice.SetVelocityParams(thorVelParams);

            if ((m_xbox == null) || !m_xbox.IsConnected) return;
            m_xboxStateLast = m_xboxState;
            m_xboxState = m_xbox.GetState();
            LeftLastStateDeadZone = LeftCurrentStateDeadZone;
            RightLastStateDeadZone = RightCurrentStateDeadZone;

            // Event handlers for buttons being pushed
            // Classic Buttons
            if (ButtonPushed(GamepadButtonFlags.A)) OnXBoxGamepadButtonPressA.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.B) && !ButtonPushed(GamepadButtonFlags.DPadUp)) OnXBoxGamepadButtonPressB.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.A)) OnXBoxGamepadButtonPressAOneShot.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.B) && !ButtonPushed(GamepadButtonFlags.DPadUp)) OnXBoxGamepadButtonPressBOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.X)) OnXBoxGamepadButtonPressX.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.X)) OnXBoxGamepadButtonPressXOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.Y)) OnXBoxGamepadButtonPressY.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.Y)) OnXBoxGamepadButtonPressYOneShot.Invoke(this, null);
            // Shoulder and Joystick Thumb Buttons

            // Shoulder buttons
            if (ButtonPushed(GamepadButtonFlags.RightShoulder)) OnXBoxGamepadButtonPressShoulderRight.Invoke(this, null);
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
            PollArduinoButton(ButtonPushed(GamepadButtonFlags.DPadDown), "A", "a", "A");
            if (ButtonPushed(GamepadButtonFlags.DPadDown)) OnXBoxGamepadButtonPressDDown.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.DPadDown)) OnXBoxGamepadButtonPressDDownOneShot.Invoke(this, null);
            var DR = GamepadButtonFlags.DPadRight;
            var DL = GamepadButtonFlags.DPadLeft;
            if (m_xboxState.Gamepad.Buttons.HasFlag(DR) && !m_xboxStateLast.Gamepad.Buttons.HasFlag(DR))
            {
                if (ButtonPushed(DR) && !(ButtonPushed(DL) || ButtonOneShot(DL))) OnXBoxGamepadButtonPressDRight.Invoke(this, null);
            }

            if (m_xboxState.Gamepad.Buttons.HasFlag(DL) && !m_xboxStateLast.Gamepad.Buttons.HasFlag(DL))
            {
                if (ButtonPushed(DL) && !(ButtonPushed(DR) || ButtonOneShot(DR))) OnXBoxGamepadButtonPressDLeft.Invoke(this, null);
            }
            if (ButtonOneShot(GamepadButtonFlags.DPadLeft)) OnXBoxGamepadButtonPressDLeftOneShot.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.DPadRight)) OnXBoxGamepadButtonPressDRightOneShot.Invoke(this, null);

            // Combo moves to save positions
            if (ButtonPushed(GamepadButtonFlags.B) && ButtonPushed(GamepadButtonFlags.DPadUp)) zaberSetCurrentPosTo(BButtonPosition);
            if (ButtonPushed(GamepadButtonFlags.X) && ButtonPushed(GamepadButtonFlags.DPadUp)) zaberSetCurrentPosTo(XButtonPosition);
            if (ButtonPushed(GamepadButtonFlags.Y) && ButtonPushed(GamepadButtonFlags.DPadUp)) zaberSetCurrentPosTo(YButtonPosition);

            // Do the triggers and shoulders
            var RTrig = m_xboxState.Gamepad.RightTrigger;
            var LTrig = m_xboxState.Gamepad.LeftTrigger;

            // Do the Right and Left Thumb X, Y
            int RX = m_xboxState.Gamepad.RightThumbX;
            int RY = m_xboxState.Gamepad.RightThumbY;
            int LX = m_xboxState.Gamepad.LeftThumbX;
            int LY = m_xboxState.Gamepad.LeftThumbY;
            double x = 0;
            double y = 0;

            //determine how far the controller is pushed
            double Lmag = Math.Sqrt(LX * LX + LY * LY);
            double Rmag = Math.Sqrt(RX * RX + RY * RY);

            //determine the direction the controller is pushed
            double normalizedLX = Convert.ToDouble(LX) / (XBOX_MAX_RANGE); //normalize to total range
            double normalizedLY = Convert.ToDouble(LY) / (XBOX_MAX_RANGE); //normalize to total range
            double normalizedRX = Convert.ToDouble(RX) / (XBOX_MAX_RANGE);
            double normalizedRY = Convert.ToDouble(RY) / (XBOX_MAX_RANGE);

            var normalizedLmag = 0.0;
            var normalizedRmag = 0.0;

            // For Consoleging -- see if the joystick register
            Console.WriteLine("The left normalized x value is \"{0}\" and y value is \"{1}\" of the Left joystick", normalizedLX, normalizedLY);
            Console.WriteLine("The normalized Right x value is \"{0}\" and y value is \"{1}\" of the Right joystick", normalizedRX, normalizedRY);

            // For Debugging -- check out properties of Triggers
            if (RTrig != 0) Console.WriteLine("RTrig value = \"{0}\"", RTrig);
            if (LTrig != 0) Console.WriteLine("LTrig value = \"{0}\"", LTrig);

            
            /************************CODE TO TEST ******************************/
            if (!m_xboxState.Gamepad.Buttons.HasFlag(DL) && m_xboxStateLast.Gamepad.Buttons.HasFlag(DL))
            {
                Action<UInt64> workDone = thorDevice.InitializeWaitHandler();
                thorDevice.Stop(workDone);
                thorDevice.ResumeMoveMessages();
            }

            if (!m_xboxState.Gamepad.Buttons.HasFlag(DR) && m_xboxStateLast.Gamepad.Buttons.HasFlag(DR))
            {
                Action<UInt64> workDone = thorDevice.InitializeWaitHandler();
                thorDevice.Stop(workDone);
                thorDevice.ResumeMoveMessages();
            }
            /************************END CODE TO TEST **************************/

            if (Lmag > lDeadZone)
            {
                //avoid nans
                x = (LX == 0) ? 1 : Convert.ToDouble(LX);
                y = (LY == 0) ? 1 : Convert.ToDouble(LY);
                double ratio = Math.Abs(x / y);

                LeftCurrentStateDeadZone = false;
                //clip the Lmag at its expected maximum value
                if (Lmag > XBOX_MAX_RANGE) Lmag = XBOX_MAX_RANGE;

                //adjust Lmag relative to the end of the dead zone
                Lmag -= lDeadZone;

                //optionally normalize the Lmag with respect to its expected range
                //giving a Lmag value of 0.0 to 1.0
                normalizedLmag = Lmag / (XBOX_MAX_RANGE - lDeadZone);

                //send the LX and LY values to move calculating function
                /** previous zaberMoveVelocity has no ratio or normalizedmag**/
                if (Math.Abs(LX) > lDeadZone) zaberMoveVelocity(2, LX, ratio, normalizedLmag, true);
                if (Math.Abs(LY) > lDeadZone) zaberMoveVelocity(3, -LY, (1/ratio), normalizedLmag, true);
           
            }
            else //if the controller is in the deadzone zero out the Lmag
            {
                LeftCurrentStateDeadZone = true;
                Lmag = 0.0;
                normalizedLmag = 0.0;

                if (LeftLastStateDeadZone == false)
                {
                    zaberStop(2);
                    zaberStop(3);
                }
            }

            //check if the controller is outside a circular dead zone for Right Thumb
            if (Rmag > rDeadZone)
            {
                x = (RX == 0) ? 1 : Convert.ToDouble(RX);
                y = (RY == 0) ? 1 : Convert.ToDouble(RY);
                double ratio = Math.Abs(x / y);

                RightCurrentStateDeadZone = false;
                //clip the Rmag at its expected maximum value
                if (Rmag > XBOX_MAX_RANGE) Rmag = XBOX_MAX_RANGE;

                //adjust Rmag relative to the end of the dead zone
                Rmag -= rDeadZone;

                //optionally normalize the Rmag with respect to its expected range
                //giving a Rmag value of 0.0 to 1.0
                normalizedRmag = Rmag / (XBOX_MAX_RANGE - rDeadZone);
                if (Math.Abs(RY) > rDeadZone) zaberMoveVelocity(4, -RY, (1/ratio), normalizedRmag, false);
            }
            else //if the controller is in the deadzone zero out the Rmag
            {
                RightCurrentStateDeadZone = true;
                Rmag = 0.0;
                normalizedRmag = 0.0;
                if (RightLastStateDeadZone == false)
                {
                    zaberStop(4);
                }
            }
        }

        // ----------------------------------------------------------------------
        // Thorlabs KCube DC Servo Functions
        // ----------------------------------------------------------------------
        public static void thorMove(IGenericAdvancedMotor device, MotorDirection direction )
        {
            try
            {
                //device.MoveTo(position, 6000);
                device.MoveContinuous(direction);
            }
            catch (Exception e)
            {
                Console.WriteLine(Convert.ToString(e));
                Console.WriteLine("Failed to move to position");
                return;
            }
        }

        // ----------------------------------------------------------------------
        // Arduino Functions
        // ----------------------------------------------------------------------

        // poll the designated button to find if the button has been pushed this sample.
        // NOTE -- If another button is hooked up in the same way, the arduino
        // will not respond as expected!!!
        void PollArduinoButton(bool buttonIsPushed, string on, string off, string inputButton)
        {
            // control arduino if it is pressed and of the right type
            if (buttonIsPushed)
            {
                //Send Trigger value to arduino
                if (Arduino.IsOpen)
                {
                    Arduino.Write(on);
                }
            }
            // if not pushed and is arduino controlled, turn off
            else
            {
                if (Arduino.IsOpen)
                {
                    Arduino.Write(off);
                }
            }
        }

        // ----------------------------------------------------------------------
        // Zaber Functions
        // ----------------------------------------------------------------------
        void zaberMoveVelocity(int device, int joyValue, double ratio, double normedMag, bool left)
        {
            string command = "";
            int vel = 0;
            int deadzone = 0;
            if (left) deadzone = lDeadZone;
            else deadzone = rDeadZone;
            if (ratio > 1) ratio = 1;
            double normedJoy = 0.0;
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
            }

            // normedJoy is a percentage of the total range outside of the deadzone
            // it will always be positive
            normedJoy = Convert.ToDouble(Math.Abs(joyValue) - deadzone) / Convert.ToDouble(XBOX_MAX_RANGE - deadzone);
            if (normedJoy > 1.0) normedJoy = 1.0;
            // modulate input joystick value so that it maps to a normalilzed exp
            normedJoy *= ratio;
            normedJoy = (Math.Exp(joystickVelocityModulator * normedJoy) - 1) / Math.Exp(joystickVelocityModulator);
            /**normedJoy = normedJoy * Convert.ToDouble(maxspeed);**/
            normedJoy = normedJoy * normedMag * Convert.ToDouble(maxspeed);
            vel = (joyValue > 0) ? Convert.ToInt32(normedJoy) : Convert.ToInt32(-normedJoy);
            command = "/" + device + " 1 move vel " + Convert.ToString(vel) + "\r\n";
            zaberPort.Write(command);
            //zaberPort.Read();
            //for erratic behavior, could try following
            //zaberPort.Drain();
           
        }

        void zaberStop(int device)
        {
            string command = "";
            command = "/" + device + " 1 stop \r\n";
            zaberPort.Write(command);
            //zaberPort.Read();
        }

        void zaberMoveStoredPosition(int[] futPos, int[] curPos)
        {
            int[] moveSequence = new int[3] { -1, -1, -1 };
            string command = "";
            if (futPos.Length != 3) Console.WriteLine("In correct number of position parameters. Please input x,y,z");
            if (futPos[0] > X_MAX) futPos[0] = X_MAX;
            if (futPos[1] > Y_MAX) futPos[1] = Y_MAX;
            if (futPos[2] > Z_MAX) futPos[2] = Z_MAX;
            moveSequence = avoidNoFlyZones(futPos, curPos);
            for (int i =0; i < futPos.Length; i++)
            {
                switch (moveSequence[i])
                {
                    case 2: // this correlates to the x axis
                        command = "/" + moveSequence[i] + " 1 move pos " + Convert.ToString(futPos[0]) + "\r\n";
                        zaberPort.Write(command);
                        break;
                    case 3: // this correlates to the y axis
                        command = "/" + moveSequence[i] + " 1 move pos " + Convert.ToString(futPos[1]) + "\r\n";
                        zaberPort.Write(command);
                        break;
                    case 4: // this correlates to the z axis
                        command = "/" + moveSequence[i] + " 1 move pos " + Convert.ToString(futPos[2]) + "\r\n";
                        zaberPort.Write(command);
                        break;
                    default:
                        Console.WriteLine("Something got fucked up in the positioning");
                        break;
                }
            }
        }

        void zaberSetCurrentPosTo(int[] pos)
        {
            /*pos[0] = axisX.GetPosition();
            zaberPort.Read();
            pos[1] = axisY.GetPosition();
            zaberPort.Read();
            pos[2] = axisZ.GetPosition();
            zaberPort.Read();*/
            zaberPort.Write("/get pos");
            string reply = Convert.ToString(zaberPort.Read());
            Console.WriteLine(reply);
            //Console.WriteLine("the position of the xaxis is ", Convert.ToString(axisX.GetPosition()));
        }

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
            // NEED TO MAP NoFlyZone WITH SETUP COMPLETED!!!
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
            Console.WriteLine("Trying to move into a dead zone!");

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
        // Stage
        // ----------------------------------------------------------------------
        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            /*DeviceManagerCLI.BuildDeviceList();*/
            //List<string> serialNumbers = DeviceManagerCLI.GetDeviceList(KCubeDCServo);

            StageInitialized = true;
        }

        //Disconnect now closes the app
        private void buttonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (m_xbox != null)
            {
                m_xbox = null;
            }
            StageInitialized = false;
            /*thorDevice.StopPolling();
            thorDevice.Disconnect();*/
            zaberStop(2);
            zaberStop(3);
            zaberStop(4);
            zaberPort.Close();
            Arduino.Close();
            App.Current.Shutdown();
        }

        private void buttonHome_click(object sender, RoutedEventArgs e)
        {
            // Need to make thorDevice public in order to access here
            //thorDevice.Home(200);
        }

        private void buttonTest_click(object sender, RoutedEventArgs e)
        {
            // Generic tester function
            StageInitialized = !StageInitialized;
        }

        private void txtConsole_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}

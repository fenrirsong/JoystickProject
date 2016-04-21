using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows;
using System.IO.Ports;

using SharpDX.XInput;

/*using Thorlabs.MotionControl.DeviceManagerCLI;
using Thorlabs.MotionControl.GenericMotorCLI;
using Thorlabs.MotionControl.GenericMotorCLI.ControlParameters;
using Thorlabs.MotionControl.GenericMotorCLI.AdvancedMotor;
using Thorlabs.MotionControl.GenericMotorCLI.Settings;
using Thorlabs.MotionControl.IntegratedStepperMotorsCLI;*/
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Input;

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

        // XBox
        private SharpDX.XInput.State m_xboxState;
        private SharpDX.XInput.State m_xboxStateLast;
        private SharpDX.XInput.Controller m_xbox = new Controller(UserIndex.One);

        private byte[] pingMessage = System.Text.Encoding.ASCII.GetBytes("ping\n");

        // Stage
        //**private string m_xAxisID = "45000001";
        //**private string m_yAxisID = "45000002";
        //private int m_typeID = 45; // Long travel stage
        //**private LongTravelStage m_deviceX = null;
        //**private LongTravelStage m_deviceY = null;
        //**private int m_waitLong = 60000;   // mS

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

        //For the Shoulders and ThumbsIn -- Triggers should be non-binary
        public event EventHandler OnXBoxGamepadButtonPressShoulderRight;
        public event EventHandler OnXBoxGamepadButtonPressShoulderRightOneShot;
        public event EventHandler OnXBoxGamepadButtonPressShoulderLeft;
        public event EventHandler OnXBoxGamepadButtonPressShoulderLeftOneShot;
        public event EventHandler OnXBoxGamepadButtonPressRightThumbIn;
        public event EventHandler OnXBoxGamepadButtonPressRightThumbInOneShot;
        public event EventHandler OnXBoxGamepadButtonPressLeftThumbIn;
        public event EventHandler OnXBoxGamepadButtonPressLeftThumbInOneShot;
        //public event EventHandler OnXBoxGamepadButtonPressTrigLeft;
        //public event EventHandler OnXBoxGamepadButtonPressTrigLeftOneShot;
        //public event EventHandler OnXBoxGamepadButtonPressTrigRight;
        //public event EventHandler OnXBoxGamepadButtonPressTrigRightOneShot;

        // ----------------------------------------------------------------------
        // Properties which support binding in the UI
        // ----------------------------------------------------------------------
        private double _posX;
        public double PosX
        {
            get
            {
                return _posX;
            }
            set
            {
                _posX = value;
                OnPropertyChanged();
            }
        }
        private double _posY;
        public double PosY
        {
            get
            {
                return _posY;
            }
            set
            {
                _posY = value;
                OnPropertyChanged();
            }
        }

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

        // For Debugging
        void DisplayControllerInformation()
        {
            var state = m_xbox.GetState();
            LeftAxis = string.Format("X: {0} Y: {1}", state.Gamepad.LeftThumbX, state.Gamepad.LeftThumbY);
            RightAxis = string.Format("X: {0} Y: {1}", state.Gamepad.RightThumbX, state.Gamepad.RightThumbX);
            Buttons = string.Format("{0}", state.Gamepad.Buttons);
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
            InitializeComponent();

            // Open the Arduino
            Arduino.PortName = "COM5";
            Arduino.BaudRate = 9600;
            Arduino.Open();

            // This guy does the gamepad polling every however many ms you want it to. 
            // The higher the sampling rate, the more likely it'll bork. YOU'VE BEEN WARNED!!
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += _timer_Tick;
            _timer.Start();

            // If the controller was not assigned to player One during initialization
            // attempt to connect to a player port
            if (!m_xbox.IsConnected)
            {
                Debug.WriteLine("XBox Controller Not Connected to One. Trying other connections");
                m_xbox = new SharpDX.XInput.Controller(UserIndex.Any);
                if (!m_xbox.IsConnected)
                {
                    Debug.WriteLine("XBox Controller Could not be connected. Goodbye!");
                    App.Current.Shutdown();
                }
            }

            Debug.WriteLine("Inside of MainWindow()");

            // Do something for this button being pressed
            // Classic Buttons
            OnXBoxGamepadButtonPressA += (s, e) =>
            {
                Debug.WriteLine("ButtonA");
                if (Arduino.IsOpen)
                {
                    Arduino.Write("A");
                }
            };
            // Do something for this button being held
            OnXBoxGamepadButtonPressAOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonAOneShot");
            };

            OnXBoxGamepadButtonPressB += (s, e) =>
            {
                Debug.WriteLine("ButtonB");
            };
            OnXBoxGamepadButtonPressBOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonBOneShot");
            };

            OnXBoxGamepadButtonPressX += (s, e) =>
            {
                Debug.WriteLine("ButtonX");
            };
            OnXBoxGamepadButtonPressXOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonXOneShot");
            };

            OnXBoxGamepadButtonPressY += (s, e) =>
            {
                Debug.WriteLine("ButtonY");
            };
            OnXBoxGamepadButtonPressYOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonYOneShot");
            };

            // DPad Buttons
            OnXBoxGamepadButtonPressDUp += (s, e) =>
            {
                Debug.WriteLine("ButtonDUp");
            };
            OnXBoxGamepadButtonPressDUpOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonDUpOneShot");
            };

            OnXBoxGamepadButtonPressDDown += (s, e) =>
            {
                Debug.WriteLine("ButtonDDown");
            };
            OnXBoxGamepadButtonPressDDownOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonDDownOneShot");
            };

            OnXBoxGamepadButtonPressDLeft += (s, e) =>
            {
                Debug.WriteLine("ButtonDLeft");
            };
            OnXBoxGamepadButtonPressDLeftOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonDLeftOneShot");
            };

            OnXBoxGamepadButtonPressDRight += (s, e) =>
            {
                Debug.WriteLine("ButtonDRight");
            };
            OnXBoxGamepadButtonPressDRightOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonDRightOneShot");
            };

            // Shoulder and ThumbsIn Buttons
            OnXBoxGamepadButtonPressShoulderRight += (s, e) =>
            {
                Debug.WriteLine("ButtonShoulderRight");
            };
            OnXBoxGamepadButtonPressShoulderRightOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonShoulderRightOneShot");
            };

            OnXBoxGamepadButtonPressShoulderLeft += (s, e) =>
            {
                Debug.WriteLine("ButtonShoulderLeft");
            };
            OnXBoxGamepadButtonPressShoulderLeftOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonShoulderLeftOneShot");
            };

            OnXBoxGamepadButtonPressRightThumbIn += (s, e) =>
            {
                Debug.WriteLine("ButtonRightThumbIn");
            };
            OnXBoxGamepadButtonPressRightThumbInOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonRightThumbInOneShot");
            };

            OnXBoxGamepadButtonPressLeftThumbIn += (s, e) =>
            {
                Debug.WriteLine("ButtonLeftThumbIn");
            };
            OnXBoxGamepadButtonPressLeftThumbInOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonLeftThumbInOneShot");
            };

            //Triggers are not binary
            /*OnXBoxGamepadButtonPressTrigRight += (s, e) =>
            {
                Debug.WriteLine("ButtonTrigRight");
            };*/
            /*OnXBoxGamepadButtonPressTrigRightOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonTrigRightOneShot");
            };*/

            //Triggers are not binary
            /*OnXBoxGamepadButtonPressTrigLeft += (s, e) =>
            {
                Debug.WriteLine("ButtonTrigLeft");
            };
            //OnXBoxGamepadButtonPressTrigLeftOneShot += (s, e) =>
            {
                Debug.WriteLine("ButtonTrigLeftOneShot");
            };*/

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
            if ((m_xbox == null) || !m_xbox.IsConnected) return;

            DisplayControllerInformation();

            m_xboxStateLast = m_xboxState;
            m_xboxState = m_xbox.GetState();

            // Event handlers for buttons being pushed
            // Classic Buttons
            PollArduinoButton(ButtonPushed(GamepadButtonFlags.A), "A", "a", "A");
            if (ButtonPushed(GamepadButtonFlags.B)) OnXBoxGamepadButtonPressB.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.A)) OnXBoxGamepadButtonPressAOneShot.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.B)) OnXBoxGamepadButtonPressBOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.X)) OnXBoxGamepadButtonPressX.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.X)) OnXBoxGamepadButtonPressXOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.Y)) OnXBoxGamepadButtonPressY.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.Y)) OnXBoxGamepadButtonPressYOneShot.Invoke(this, null);
            // Shoulder and Joystick Thumb Buttons
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
            if (ButtonPushed(GamepadButtonFlags.DPadDown)) OnXBoxGamepadButtonPressDDown.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.DPadDown)) OnXBoxGamepadButtonPressDDownOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.DPadLeft)) OnXBoxGamepadButtonPressDLeft.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.DPadLeft)) OnXBoxGamepadButtonPressDLeftOneShot.Invoke(this, null);
            if (ButtonPushed(GamepadButtonFlags.DPadRight)) OnXBoxGamepadButtonPressDRight.Invoke(this, null);
            if (ButtonOneShot(GamepadButtonFlags.DPadRight)) OnXBoxGamepadButtonPressDRightOneShot.Invoke(this, null);

            // Do the triggers and shoulders
            var RTrig = m_xboxState.Gamepad.RightTrigger;
            var LTrig = m_xboxState.Gamepad.LeftTrigger;

            // Do the Right and Left Thumb X, Y
            var RX = m_xboxState.Gamepad.RightThumbX;
            var RY = m_xboxState.Gamepad.RightThumbY;
            var LX = m_xboxState.Gamepad.LeftThumbX;
            var LY = m_xboxState.Gamepad.LeftThumbY;

            //determine how far the controller is pushed
            var Lmag = Math.Sqrt(LX * LX + LY * LY);
            var Rmag = Math.Sqrt(RX * RX + RY * RY);

            //determine the direction the controller is pushed
            var normalizedLX = LX / Lmag;
            var normalizedLY = LY / Lmag;
            var normalizedRX = RX / Rmag;
            var normalizedRY = RY / Rmag;

            var normalizedLmag = 0.0;
            var normalizedRmag = 0.0;

            // For Debugging -- see if the joystick register
            /*if (LX != 0 || LY != 0)
            {
                Debug.WriteLine("The normalized x value is \"{0}\" and y value is \"{1}\" of the Left joystick", normalizedLX, normalizedLY);
            }
            if (RX != 0 || RY != 0)
            {
                Debug.WriteLine("The normalized x value is \"{0}\" and y value is \"{1}\" of the Right joystick", normalizedRX, normalizedRY);
            }*/

            // For Debugging -- check out properties of Triggers
            if (RTrig != 0) Debug.WriteLine("RTrig value = \"{0}\"", RTrig);
            if (LTrig != 0) Debug.WriteLine("LTrig value = \"{0}\"", LTrig);

            //check if the controller is outside a circular dead zone for Left Thumb
            if (Lmag > Gamepad.LeftThumbDeadZone)
            {
                //clip the Lmag at its expected maximum value
                if (Lmag > 32767) Lmag = 32767;

                //adjust Lmag relative to the end of the dead zone
                Lmag -= Gamepad.LeftThumbDeadZone;

                //optionally normalize the Lmag with respect to its expected range
                //giving a Lmag value of 0.0 to 1.0
                normalizedLmag = Lmag / (32767 - Gamepad.LeftThumbDeadZone);
            }
            else //if the controller is in the deadzone zero out the Lmag
            {
                Lmag = 0.0;
                normalizedLmag = 0.0;
                // m_deviceX.Stop(6000);
            }

            //check if the controller is outside a circular dead zone for Right Thumb
            if (Rmag > Gamepad.RightThumbDeadZone)
            {
                //clip the Rmag at its expected maximum value
                if (Rmag > 32767) Rmag = 32767;

                //adjust Rmag relative to the end of the dead zone
                Rmag -= Gamepad.RightThumbDeadZone;

                //optionally normalize the Rmag with respect to its expected range
                //giving a Rmag value of 0.0 to 1.0
                normalizedRmag = Rmag / (32767 - Gamepad.RightThumbDeadZone);
            }
            else //if the controller is in the deadzone zero out the Rmag
            {
                Rmag = 0.0;
                normalizedRmag = 0.0;
                // m_deviceY.Stop(6000);
            }

            //**m_deviceX.MoveContinuous(MotorDirection.Forward);
            //**m_deviceY.MoveContinuous(MotorDirection.Forward);
        }

        // ----------------------------------------------------------------------
        // Arduino
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
        // Stage
        // ----------------------------------------------------------------------

        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            //PosX = 100.999;
            //return;

            //**if (m_deviceX != null) return;

            //if (m_xbox != null) FindXBoxController();

            // Try to create synthetic stages (but Create will fail!)d
            //var sx = DeviceManagerCLI.RegisterSimulation(xAxisID, typeID, "X axis");
            //var sy = DeviceManagerCLI.RegisterSimulation(yAxisID, typeID, "Y axis");

            //**DeviceManagerCLI.BuildDeviceList();
            //**List<string> serialNumbers = DeviceManagerCLI.GetDeviceList(LongTravelStage.DevicePrefix);

            // X axis -------------------------------------------------------
            //**m_deviceX = LongTravelStage.CreateLongTravelStage(m_xAxisID);
            //**m_deviceX.Connect(m_xAxisID);
            //**var diX = m_deviceX.GetDeviceInfo();

            // wait for the device settings to initialize
            //**if (!m_deviceX.IsSettingsInitialized())
            //**{
            //**m_deviceX.WaitForSettingsInitialized(5000);
            //**}

            // start the device polling            
            //**m_deviceX.StartPolling(250);
            //**m_deviceX.EnableDevice();

            // Y axis -------------------------------------------------------
            //**m_deviceY = LongTravelStage.CreateLongTravelStage(m_yAxisID);
            //**m_deviceY.Connect(m_xAxisID);
            //**var diY = m_deviceY.GetDeviceInfo();

            // wait for the device settings to initialize
            //**if (!m_deviceY.IsSettingsInitialized())
            //**{
            //**m_deviceY.WaitForSettingsInitialized(5000);
            //**}

            // start the device polling            
            //**m_deviceY.StartPolling(250);
            //**m_deviceY.EnableDevice();

            StageInitialized = true;
        }

        //Disconnect now closes the app
        private void buttonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            //**if (m_deviceX == null) return;

            //**m_deviceX.StopPolling();
            //**m_deviceX.Disconnect();
            //**m_deviceX = null;

            if (m_xbox != null)
            {
                m_xbox = null;
            }
            StageInitialized = false;
            Arduino.Close();
            App.Current.Shutdown();
        }

        private void buttonHome_click(object sender, RoutedEventArgs e)
        {
            //**m_deviceX.Home(m_waitLong);
            //**m_deviceY.Home(m_waitLong);
        }

        private void buttonTest_click(object sender, RoutedEventArgs e)
        {
            // Generic tester function
            StageInitialized = !StageInitialized;
        }
    }
}

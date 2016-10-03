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
        // Counters, GUI
        DispatcherTimer _gamepadTimer = new DispatcherTimer();
        DispatcherTimer _dataTimer = new DispatcherTimer();
        private byte[] pingMessage = System.Text.Encoding.ASCII.GetBytes("ping\n");
        private double _axisMax;
        private double _axisMin;
        public int visibleDataPoints = 60;
        public string bButtonPosition = "null";
        public string xButtonPosition = "null";
        public string yButtonPosition = "null";
        public event PropertyChangedEventHandler PropertyChanged;
        //from wpfshapeapp
        public string gridPlacementConfig;
        public int numberOfSticks;
        public int numberOfGridsPerStick;
        public int cassettePositionNumber;
        public int totalNumberOfGrids;
        public int gridLabelCounter;
        public int sectionStartingNo;
        public int sectionNumber;
        public int cassetteNumber;
        public double stickWidth;
        public int gridStickWidth = 45;
        public int gridDiameter = 35;
        public System.Windows.Media.Color gridColor = Colors.AntiqueWhite;
        public System.Windows.Media.Color loopColor = Colors.LightGray;
        public System.Windows.Media.Brush LoopColor = System.Windows.Media.Brushes.LightGray;
        public System.Windows.Media.Brush StickColor = System.Windows.Media.Brushes.LightGray;
        public System.Windows.Media.Color canvasColor = Colors.Gray;
        public System.Windows.Media.Brush CanvasColor = System.Windows.Media.Brushes.Gray;
        public System.Windows.Media.Color gridReadyColor = Colors.Green;
        public System.Windows.Media.Color gridDeadColor = Colors.Red;
        public System.Windows.Media.Color boatColor = Colors.Black;
        public System.Windows.Media.Brush BoatColor = System.Windows.Media.Brushes.Black;
        public System.Windows.Media.Brush SectionColor = System.Windows.Media.Brushes.Wheat;
        public System.Windows.Media.Color promptColor = Colors.Red;

        public int loopDiameter = 45;
        public int appDiameter = 30;
        public int boatRight = 5;
        public int boatTop = 5;
        public int loopRight = 45;
        public int loopTop = 65;
        public int stickLeft = 5;
        public int stickTop = 5;
        public const string rowsColConfig = "Rows then Columns";
        public const string colsRowConfig = "Columns then Rows";
        public const string staggered = "Sticks as Consumables(staggered)";
        // Files
        public string sensorFile;
        public string buttonFile;
        public string zaberFile; //for debugging mostly...
        public string sectionPositionFile;
        public string resourcePathRoot = @"C:\Users\Public\XBoxStageVersions\XBoxStageRefactor\XBoxStageMaster\XBoxStage\Resources\";
        public string outputPathRoot = @"C:\Users\Public\SerialSectionSessions\";
        // Stick Stuff -- make this into a data type later for sticks/consumable sticks
        public int numberOfCols = 8; // this is specifically made for sticks as consumables
        //public int numberOfSticks = 4; //this is the number of sticks on the casette
        public double xGridDisplacement = 5.3; //these are the relative positions of the grids
        public double yGridStaggerDisplacement = 2.0; // on the consumable sticks
        public double yGridDisplacement = 8.6; // these are the relative positions of the loops
        public double xLoopDisplacement = 3.6; // on the mats, in mm
        public double yLoopDisplacement = 3.6; // 
        public bool sticksAsConsumables = true;
        public bool stickInfoInitialized = false;
        // FSM 
        public int State = -1;
        public int LastState = -1;
        public const int INITIALIZATION_STATE = 0;
        public const int PICKING_UP_LOOP_STATE = 1;
        public const int PICKING_UP_SECTION_STATE = 2;
        public const int PLACING_SECTION_STATE = 3;
        public bool bReady;
        public bool xReady;
        public bool yReady;
        public int loopPickUpCount; //counter for loop being picked up

        // Thorlabs Actuator (serial number provided is specific to my device)
        public KCubeDCServo thorDevice;
        public string serialNo = "27000117";
        public int thorPollRate = 20;
        public decimal thorDisplacement = 1m;

        // Arduino and sensor output stuff -- use the sketch WindHallAirTempV2.0.ino
        // to control the Thorlabs and solenoid air puffer
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
        void _gamepadTick(object sender, EventArgs e)
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
            string buttonMessage = "";
            string stateMessage = "";
            switch (button)
            {
                case "B":
                    stateMessage = "Fail to drop off section. Film potentially broken at " + cassettePositionNumber.ToString() + ". Advancing to next drop off location";
                    stateMessage = (LastState == PICKING_UP_LOOP_STATE ? "Fail to place section on stick. Returning to place section" : LastState == PICKING_UP_SECTION_STATE ? "Picked up section, advancing to stick drop off." : LastState == PLACING_SECTION_STATE ? stateMessage : "LastState Undefined") ;
                    Array.Copy(BButtonPosition, buttonPosition, 3);
                    break;
                case "X":
                    stateMessage = "Section number " + sectionNumber.ToString() + " placed at " + cassettePositionNumber.ToString() + ", picking up next loop";
                    stateMessage = (LastState == PICKING_UP_LOOP_STATE ? "Failed to pick up last loop, advancing to next loop" : LastState == PICKING_UP_SECTION_STATE ? "Failed to pick up section, returning to pick up loop" : LastState == PLACING_SECTION_STATE ? stateMessage : "LastState Undefined");
                    Array.Copy(XButtonPosition, buttonPosition, 3);
                    break;
                case "Y":
                    stateMessage = "Attempting to remove section number " + sectionNumber.ToString() + " from boat";
                    stateMessage = (LastState == PICKING_UP_LOOP_STATE ? stateMessage : LastState == PICKING_UP_SECTION_STATE ? "Failed to pick up section, trying again" : LastState == PLACING_SECTION_STATE ? "Failed to pick up section? Returning to boat" : "LastState Undefined");
                    Array.Copy(YButtonPosition, buttonPosition, 3);
                    break;
                case "DownB":
                    stateMessage = "Overshot stick downoff point, backing up to cassettePosition " + cassettePositionNumber.ToString();
                    Array.Copy(BButtonPosition, buttonPosition, 3);
                    break;
                case "DownX":
                    stateMessage = "Overshot loop pickup point, backing up loop pick up position.";
                    Array.Copy(XButtonPosition, buttonPosition, 3);
                    break;
                default:
                    Console.WriteLine("Button Pressed not registered");
                    return;
            }
            buttonMessage = button + ", ";
            Console.WriteLine(buttonMessage);
            curPos = zaberGetCurrentPos();
            Console.WriteLine("curPos:({0}, {1}, {2})", curPos[0], curPos[1], curPos[2]);
            Console.WriteLine("futPos: ({0}, {1}, {2})", buttonPosition[0], buttonPosition[1], buttonPosition[2]);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(buttonFile, true))
            {
                file.WriteLine("");
                file.Write(GetTimeStamp(DateTime.Now));
                file.Write(", {0}, {1}, {2}, {3}, ", button, buttonPosition[0], buttonPosition[1], buttonPosition[2]);
                file.Write("{0}, {1}, {2}, {3}, {4}, {5}", curPos[0], curPos[1], curPos[2], LastState, State, stateMessage);
            }
        }

        //Function that records the sections at given cassette position
        void recordSectionAtPosition()
        {
            string positionData = "";
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(sectionPositionFile, true))
            {
                file.WriteLine("");
                file.Write(GetTimeStamp(DateTime.Now));
                positionData = "cassette Number: "+ cassetteNumber +"\t sectionNumberAtCassettePosition[" + cassettePositionNumber + "]: " + sectionNumber;
                file.WriteLine(positionData);
            }
            return;
        }

        //save the image of the cassette before closing program or moving onto the next cassette
        private void saveCanvasImage()
        {
            string fileName = outputPathRoot + "cassetteNo" + cassetteNumber + "At" + GetTimeAndDate(DateTime.Now) + ".png";
            System.Windows.Media.Imaging.RenderTargetBitmap rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)canvasArea.RenderSize.Width,
                (int)canvasArea.RenderSize.Height, 96d, 96d, System.Windows.Media.PixelFormats.Default);
            rtb.Render(canvasArea);

            System.Windows.Media.Imaging.BitmapEncoder pngEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            pngEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));


            using (var fs = System.IO.File.OpenWrite(fileName))
            {
                pngEncoder.Save(fs);
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
            m_xbox = new Controller(UserIndex.One);
            if (m_xbox.IsConnected) return;
            System.Windows.MessageBox.Show("Controller is not connected");
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
                (sender as System.Windows.Forms.CheckBox).Checked = false;
                bButtonPosition = "(0, 0, 0)";
                BBox.Text = bButtonPosition;
                bReady = true;
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
                xButtonPosition = "(0, 0, 0)";
                XBox.Text = xButtonPosition;
                xReady = true;
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
                yButtonPosition = "(0, 0, 0)";
                YBox.Text = yButtonPosition;
                yReady = true;
            }
            else
            {
                (sender as System.Windows.Forms.CheckBox).Checked = true;
            }
        }

        public MainWindow() 
        {
            // This stuff loads the window and closes it
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            InitializeComponent();

            //Initialize Controlled Parts
            thorInitialize();
            arduinoInitialize();
            zaberInitialize();

            //Start thorDevice polling
            thorDevice.StartPolling(thorPollRate);

            //Initialize counters for pick up and drop off
            State = INITIALIZATION_STATE;
            bReady = false; //these guys keep track of what buttons are initialized
            xReady = false;
            yReady = false;
            loopPickUpCount = 0; // initialize counters to zero
            cassettePositionNumber = 0;

            // This guy does the gamepad polling every however many ms you want it to. 
            // The higher the sampling rate, the more likely it'll bork. YOU'VE BEEN WARNED!!
            _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(POLL_RATE) };
            _gamepadTimer.Tick += _gamepadTick;
            _gamepadTimer.Start();

            // GUI Stuff -- set all the bindings up
            InitializeComponent();
            cassetteNumber = 1;
            numberOfSticks = 4;
            cassettePositionNumber = -1;
            sectionNumber = sectionStartingNo;
            textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding(numberOfSticks.ToString())
            {
                Source = numberOfSticks.ToString(),
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            textBox.Text = numberOfSticks.ToString();

            numberOfGridsPerStick = 16;
            textBoxGrids.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding(numberOfGridsPerStick.ToString())
            {
                Source = numberOfGridsPerStick.ToString(),
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            textBoxGrids.Text = numberOfGridsPerStick.ToString();

            comboBox.SetBinding(System.Windows.Controls.ComboBox.TextProperty, new System.Windows.Data.Binding(gridPlacementConfig)
            {
                Source = gridPlacementConfig,
                Mode = System.Windows.Data.BindingMode.OneWay
            });

            textBoxXGridSpace.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding(xGridDisplacement.ToString())
            {
                Source = xGridDisplacement.ToString(),
                Mode = System.Windows.Data.BindingMode.OneWay
            });
            textBoxXGridSpace.Text = xGridDisplacement.ToString();

            textBoxYGridSpace.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding(yGridDisplacement.ToString())
            {
                Source = yGridDisplacement.ToString(),
                Mode = System.Windows.Data.BindingMode.OneWay
            });
            textBoxYGridSpace.Text = yGridDisplacement.ToString();

            textBoxStaggerGridSpace.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding(yGridStaggerDisplacement.ToString())
            {
                Source = yGridStaggerDisplacement.ToString(),
                Mode = System.Windows.Data.BindingMode.OneWay
            });
            textBoxStaggerGridSpace.Text = yGridStaggerDisplacement.ToString();

            textBoxSectionStartNo.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding(sectionStartingNo.ToString())
            {
                Source = sectionStartingNo.ToString(),
                Mode = System.Windows.Data.BindingMode.OneWay
            });
            textBoxSectionStartNo.Text = sectionStartingNo.ToString();

            gridLabelCounter = 0;

            // Initialize filenames and data structures for sensor data
            string timeAndDate = GetTimeAndDate(DateTime.Now);
            sensorFile = outputPathRoot + "sensorOutput" + timeAndDate + ".txt"; 
            buttonFile = outputPathRoot + "buttonOutput" + timeAndDate + ".txt";
            zaberFile = outputPathRoot + "zaberOutput" + timeAndDate + ".txt";
            sectionPositionFile = outputPathRoot + "sectionsAtPositions" + timeAndDate + ".txt";

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(buttonFile, true))
            {
                file.Write("TimeStamp, ButtonPressed, XfuturePosition, YfuturePosition, ZfuturePosition, XcurrentPosition, YcurrentPosition, ZcurrentPosition, LastState, State, State Change Message");
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

            OnXBoxGamepadButtonPressB += (s, e) => //Button B places the loops
            {
                double[] pos = new double[2];
                LastState = State; 
                State = PLACING_SECTION_STATE;
                recordButtonPress("B");
                switch (LastState)
                {
                    case PICKING_UP_LOOP_STATE: // corresponds to action "B3"
                        sectionNumber--; //QUESTIONABLE
                        zaberMoveNextDropOffPoint();
                        drawLoop();
                        drawSectionInLoop();
                        cassettePositionNumber--;
                        if (sectionNumber < sectionStartingNo) sectionNumber = sectionStartingNo;
                        if (cassettePositionNumber < -1)
                        {
                            cassettePositionNumber = -1;
                        }
                        pos = getCanvasPositionForCassettePositionNumber(cassettePositionNumber);
                        colorGridAtPositionXY(gridReadyColor, pos[0], pos[1]);
                        break;
                    case PICKING_UP_SECTION_STATE: // corresponds to action "B1"
                        zaberMoveNextDropOffPoint();
                        //update available actions
                        removeSectionInBoat();
                        drawSectionInLoop();
                        //turn grid it's about to be placed on green
                        pos = getCanvasPositionForCassettePositionNumber(cassettePositionNumber);
                        colorGridAtPositionXY(gridReadyColor, pos[0], pos[1]);
                        break;
                    case PLACING_SECTION_STATE: // corresponds to action "B2"
                        zaberMoveNextDropOffPoint();
                        pos = getCanvasPositionForCassettePositionNumber(cassettePositionNumber);
                        colorGridAtPositionXY(gridDeadColor, pos[0], pos[1]);
                        cassettePositionNumber++;
                        pos = getCanvasPositionForCassettePositionNumber(cassettePositionNumber);
                        colorGridAtPositionXY(gridReadyColor, pos[0], pos[1]);
                        if (cassettePositionNumber >= totalNumberOfGrids) //show dialogue box
                        {
                            reachedEndOfCassette();
                        }
                        break;
                }
            };

            OnXBoxGamepadButtonPressX += (s, e) => //Button X picks up the loops
            {
                LastState = State; 
                State = PICKING_UP_LOOP_STATE;
                recordButtonPress("X");
                zaberMoveNextPickUpPoint();
                loopPickUpCount++;
                switch (LastState)
                {
                    case PICKING_UP_LOOP_STATE: //corresponds to action "X2"
                        break;
                    case PICKING_UP_SECTION_STATE: //corresponds to action "X1"
                        removeLoop();
                        removeSectionFromLoop();
                        break;
                    case PLACING_SECTION_STATE: //corresponds to action "X3"
                        double[] pos = new double[2];
                        removeLoop();
                        //turn grid gridColor and label the grid with section number
                        if (cassettePositionNumber >= 0)
                        {
                            pos = getCanvasPositionForCassettePositionNumber(cassettePositionNumber);
                            colorGridAtPositionXY(gridColor, pos[0], pos[1]);
                            labelGrid(pos[0] + gridDiameter / 2, pos[1] + gridDiameter / 4, sectionNumber.ToString(), Colors.Black);
                            sectionNumber++;
                        }
                        cassettePositionNumber++;
                        if (cassettePositionNumber >= totalNumberOfGrids) //show dialogue box
                        {
                            reachedEndOfCassette();
                        }
                        recordSectionAtPosition();
                        break;
                }
            };

            OnXBoxGamepadButtonPressY += (s, e) => //Button Y picks up the sections
            {
                LastState = State; 
                State = PICKING_UP_SECTION_STATE;
                recordButtonPress("Y");
                zaberMoveStoredPositionAllAtOnce(YButtonPosition);
                switch (LastState)
                {
                    case PICKING_UP_LOOP_STATE: //corresponds to action "Y1"
                        drawSectionInBoat();
                        drawLoop();
                        break;
                    case PICKING_UP_SECTION_STATE: //corresponds to action "Y2"
                        break;
                    case PLACING_SECTION_STATE: //corresponds to action "Y3"
                        break;
                }
            };

            // maybe make these two only invokable when in their buttons' specific state
            MoveToLastDropOffPosition += (s, e) => //Set back loopDropOff counter and move to previous loop position
            {
                LastState = State;
                State = PLACING_SECTION_STATE;
                zaberMoveNextDropOffPoint();
                double[] pos = new double[2];
                pos = getCanvasPositionForCassettePositionNumber(cassettePositionNumber);
                colorGridAtPositionXY(gridColor, pos[0], pos[1]);
                cassettePositionNumber -= 2;
                //sectionNumber--;
                if (sectionNumber < sectionStartingNo) sectionNumber = sectionStartingNo;
                if (cassettePositionNumber < -1)
                {
                    cassettePositionNumber = -1;
                }
                pos = getCanvasPositionForCassettePositionNumber(cassettePositionNumber);
                colorGridAtPositionXY(gridReadyColor, pos[0], pos[1]);
            };

            MoveToLastPickUpPosition += (s, e) => //Set back loopPickUp counter and move to previous loop position
            {
                LastState = State;
                State = PICKING_UP_LOOP_STATE;
                loopPickUpCount -= 2;
                zaberMoveNextPickUpPoint();
            };
        }

        // update in the number of sticks
        private void UserChangedNumberOfSticks_TextChanged(object sender, TextChangedEventArgs e)
        {
            Int32.TryParse(textBox.Text, out numberOfSticks);
            if (numberOfSticks > 50) numberOfSticks = 50;
            updateCassette();
            isGridConfigFinished();
        }

        // update in the number of grids
        private void UserChangedNumberOfGrids_TextChanged(object sender, TextChangedEventArgs e)
        {
            Int32.TryParse(textBoxGrids.Text, out numberOfGridsPerStick);
            if (numberOfGridsPerStick > 50) numberOfGridsPerStick = 50;
            totalNumberOfGrids = numberOfGridsPerStick * numberOfSticks;
            updateCassette();
            isGridConfigFinished();
        }

        // update the x displacement between grids
        private void UserChangedXGridSpace_TextChanged(object sender, TextChangedEventArgs e)
        {
            Double.TryParse(textBoxXGridSpace.Text, out xGridDisplacement);
            updateCassette();
            isGridConfigFinished();
        }

        // update the x displacement between grids
        private void UserChangedYGridSpace_TextChanged(object sender, TextChangedEventArgs e)
        {
            Double.TryParse(textBoxYGridSpace.Text, out yGridDisplacement);
            updateCassette();
            isGridConfigFinished();
        }

        // update the staggered y displacement 
        private void UserChangedStaggerGridSpace_TextChanged(object sender, TextChangedEventArgs e)
        {
            Double.TryParse(textBoxStaggerGridSpace.Text, out yGridStaggerDisplacement);
            updateCassette();
            isGridConfigFinished();
        }

        // update the section start number
        private void UserChangedStartingNo_TextChanged(object sender, TextChangedEventArgs e)
        {
            Int32.TryParse(textBoxSectionStartNo.Text, out sectionStartingNo);
            updateCassette();
            isGridConfigFinished();
            sectionNumber = sectionStartingNo;
        }

        // check to see if there's enough information to start the FSM
        private void isGridConfigFinished()
        {
            if (numberOfSticks != 0 && numberOfGridsPerStick != 0 && xGridDisplacement != 0 && yGridDisplacement != 0)
            {
                if (gridPlacementConfig != staggered)
                {
                    StartButtonInitialization.Visibility = System.Windows.Visibility.Visible;
                    return;
                }
                else if (gridPlacementConfig == staggered && yGridStaggerDisplacement != 0)
                {
                    StartButtonInitialization.Visibility = System.Windows.Visibility.Visible;
                    return;
                }
            }
            StartButtonInitialization.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        // hide the grid parameters on the GUI
        public void hideGridParameters()
        {
            comboBox.Visibility = System.Windows.Visibility.Collapsed;
            textBlockSectionStartNo.Visibility = System.Windows.Visibility.Collapsed;
            textBoxSectionStartNo.Visibility = System.Windows.Visibility.Collapsed;
            textBlock.Visibility = System.Windows.Visibility.Collapsed;
            textBox.Visibility = System.Windows.Visibility.Collapsed;
            textBlockGrids.Visibility = System.Windows.Visibility.Collapsed;
            textBoxGrids.Visibility = System.Windows.Visibility.Collapsed;
            textBlockXGridSpace.Visibility = System.Windows.Visibility.Collapsed;
            textBlockYGridSpace.Visibility = System.Windows.Visibility.Collapsed;
            textBoxXGridSpace.Visibility = System.Windows.Visibility.Collapsed;
            textBoxYGridSpace.Visibility = System.Windows.Visibility.Collapsed;
            textBlockStaggerGridSpace.Visibility = System.Windows.Visibility.Collapsed;
            textBoxStaggerGridSpace.Visibility = System.Windows.Visibility.Collapsed;
            StartButtonInitialization.Visibility = System.Windows.Visibility.Collapsed;
            textBlock1.Visibility = System.Windows.Visibility.Collapsed;
            clearLines();
            clearLabels();
        }

        //show all the important post-grid initialization stuff
        private void showDataAndCassette()
        {
            stackPanel.Visibility = System.Windows.Visibility.Visible;
            SensorDataView.Visibility = System.Windows.Visibility.Visible;
        }

        // begin the FSM
        public void FinishedGridConfig_Click(object sender, RoutedEventArgs e)
        {
            //change visibility of the different GUI elements
            hideGridParameters();
            showDataAndCassette();
            drawBoat();
            //draw on canvas where to initialize loop pick up/section drop off/boat area
            initializationPrompt("b");
        }

        private void initializationPrompt(string button)
        {
            TextBlock prompt = new TextBlock();
            int x, y;
            switch (button)
            {
                case "b":
                    prompt.Text = "Please move foreceps to first cassette position.\nOnce you are at the desired location, save position by pressing XBox 'Save' and XBox 'B' buttons simultaneously.";
                    x = stickLeft;
                    Canvas.SetLeft(prompt, x);
                    y = (int)stickWidth + stickTop;
                    break;
                case "x":
                    prompt.Text = "Please move foreceps to first loop pick up position.\nOnce you are at the desired location, save position by pressing XBox 'Save' and XBox 'X' buttons simultaneously.";
                    x = 150;
                    Canvas.SetLeft(prompt, x);
                    y = 100;
                    break;
                case "y":
                    prompt.Text = "Please move foreceps to the section pick up position.\nOnce you are at the desired location, save position by pressing XBox 'Save' and XBox 'Y' buttons simultaneously.";
                    x = boatRight + 50;
                    Canvas.SetRight(prompt, x);
                    y = boatTop + 30;
                    break;
                default:
                    prompt.Text = "";
                    x = 0;
                    Canvas.SetLeft(prompt, x);
                    y = 0;
                    break;
            }
            prompt.Foreground = new SolidColorBrush(promptColor);
            Canvas.SetTop(prompt, y);
            canvasArea.Children.Add(prompt);
        }

        private void reachedEndOfCassette()
        {
            MessageBoxResult nextCassette = System.Windows.MessageBox.Show("You have reached the end of the cassette. \nContinue to next cassette?", "Reload Cassette", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            switch (nextCassette)
            {
                case MessageBoxResult.Yes: //keep numbers, save canvas, reset cassette, increment cassette number
                    cassetteNumber++;
                    cassettePositionNumber = -1;
                    saveCanvasImage();
                    updateCassette();
                    hideGridParameters();
                    recordSectionAtPosition();
                    drawBoat();
                    break;
                case MessageBoxResult.No: //save canvas and data, exit program
                    saveCanvasImage();
                    recordSectionAtPosition();
                    Environment.Exit(0);
                    return;
                case MessageBoxResult.Cancel: //continue
                    break;
            }
        }

        // automatic selection when box is loaded
        private void comboBox_Loaded(object sender, RoutedEventArgs e)
        {
            List<string> gridConfigOptions = new List<string>();
            gridConfigOptions.Add(rowsColConfig);
            gridConfigOptions.Add(colsRowConfig);
            gridConfigOptions.Add(staggered);

            var comboBox = sender as System.Windows.Controls.ComboBox;

            comboBox.ItemsSource = gridConfigOptions;
            comboBox.SelectedIndex = 2;
            updateCassette();
        }

        // update stick type when changed
        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as System.Windows.Controls.ComboBox;
            gridPlacementConfig = comboBox.SelectedItem as string;
            this.Title = gridPlacementConfig;
            if (gridPlacementConfig == staggered) //make the staggering info visible
            {
                textBoxStaggerGridSpace.Visibility = System.Windows.Visibility.Visible;
                textBlockStaggerGridSpace.Visibility = System.Windows.Visibility.Visible;
            }
            else if (gridPlacementConfig != staggered)
            {
                textBoxStaggerGridSpace.Visibility = System.Windows.Visibility.Collapsed;
                textBlockStaggerGridSpace.Visibility = System.Windows.Visibility.Collapsed;
            }
            updateCassette();
        }

        // update cassette
        private void updateCassette()
        {
            clearCanvas();
            stickWidth = numberOfGridsPerStick * gridStickWidth;
            totalNumberOfGrids = numberOfGridsPerStick * numberOfSticks;
            switch (gridPlacementConfig)
            {
                case rowsColConfig:
                    if (numberOfGridsPerStick > 0)
                    {
                        drawSticks();
                        labelGridSticks();
                        drawStickDistLines();
                    }
                    break;
                case colsRowConfig:
                    if (numberOfGridsPerStick > 0)
                    {
                        drawSticks();
                        labelGridSticks();
                        drawStickDistLines();
                    }
                    break;
                case staggered:
                    stickWidth = (numberOfGridsPerStick) * gridStickWidth * .8;
                    if (numberOfGridsPerStick > 0)
                    {
                        drawSticks();
                        drawStickDistLines();
                    }
                    break;
            }
        }

        //clear the canvas of labels and shapes  -- GOOD
        private void clearCanvas()
        {
            clearLabels();
            clearLines();
            clearShapes();
            gridLabelCounter = 0;
        }

        //clear all the lines on the canvas
        private void clearLines()
        {
            var lines = canvasArea.Children.OfType<Line>().ToList();
            foreach (var line in lines)
            {
                canvasArea.Children.Remove(line);
            }
        }

        //clear all the labels on the canvas
        private void clearLabels()
        {
            var labels = canvasArea.Children.OfType<TextBlock>().ToList();
            foreach (var label in labels)
            {
                canvasArea.Children.Remove(label);
            }
        }

        //clear the distance showing labels on the canvas
        private void clearDistLabels()
        {
            var labels = canvasArea.Children.OfType<TextBlock>().ToList();
            foreach (var label in labels)
            {
                if (label.Text.Contains("mm")) canvasArea.Children.Remove(label);
            }
        }

        //clear all the shapes on the canvas
        private void clearShapes()
        {
            var shapes = canvasArea.Children.OfType<Shape>().ToList();
            foreach (var shape in shapes)
            {
                canvasArea.Children.Remove(shape);
            }
        }

        // draw the boat
        private void drawBoat()
        {
            Shape boat = new System.Windows.Shapes.Rectangle() { Fill = BoatColor, Height = 200, Width = 100, RadiusX = 2, RadiusY = 2 };
            Canvas.SetRight(boat, boatRight);
            Canvas.SetTop(boat, boatTop);
            canvasArea.Children.Add(boat);
            TextBlock boatText = new TextBlock();
            boatText.Text = "Boat";
            boatText.Foreground = new SolidColorBrush(Colors.White);
            Canvas.SetRight(boatText, boatRight + 40);
            Canvas.SetTop(boatText, boatTop + 10);
            canvasArea.Children.Add(boatText);
        }

        //draw a section floating in the boat
        private void drawSectionInBoat()
        {
            Shape section = new System.Windows.Shapes.Rectangle() { Fill = SectionColor, Height = 10, Width = 10, RadiusX = 2, RadiusY = 2 };
            Canvas.SetRight(section, boatRight + 50);
            Canvas.SetTop(section, boatTop + 40);
            canvasArea.Children.Add(section);
        }

        //erase the section in the boat
        private void removeSectionInBoat()
        {
            Shape section = new System.Windows.Shapes.Rectangle() { Fill = BoatColor, Height = 12, Width = 12, RadiusX = 2, RadiusY = 2 };
            Canvas.SetRight(section, boatRight + 50);
            Canvas.SetTop(section, boatTop + 40);
            canvasArea.Children.Add(section);
        }

        // draw the section in the loop
        private void drawSectionInLoop()
        {
            Shape section = new System.Windows.Shapes.Rectangle() { Fill = SectionColor, Height = 10, Width = 10, RadiusX = 2, RadiusY = 2 };
            Canvas.SetRight(section, loopRight + loopDiameter / 2 - 4);
            Canvas.SetTop(section, loopTop + loopDiameter / 2 - 4);
            canvasArea.Children.Add(section);
        }

        //erase the section from the loop
        private void removeSectionFromLoop()
        {
            Shape section = new System.Windows.Shapes.Rectangle() { Fill = BoatColor, Height = 10, Width = 10, RadiusX = 0, RadiusY = 0 };
            Canvas.SetRight(section, loopRight + loopDiameter / 2 - 4);
            Canvas.SetTop(section, loopTop + loopDiameter / 2 - 4);
            canvasArea.Children.Add(section);
        }

        //draw the loop
        private void drawLoop()
        {
            Shape loop = new Ellipse() { Height = loopDiameter, Width = loopDiameter };
            RadialGradientBrush brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(loopColor, 0.250));
            brush.GradientStops.Add(new GradientStop(loopColor, 0.100));
            brush.GradientStops.Add(new GradientStop(loopColor, 8));
            loop.Fill = brush;
            Canvas.SetRight(loop, loopRight);
            Canvas.SetTop(loop, loopTop);
            canvasArea.Children.Add(loop);

            Shape handle = new System.Windows.Shapes.Rectangle() { Fill = LoopColor, Height = 10, Width = 10, RadiusX = 0, RadiusY = 0 };
            Canvas.SetRight(handle, loopRight + loopDiameter);
            Canvas.SetTop(handle, loopTop + loopDiameter / 2 - 4);
            canvasArea.Children.Add(handle);

            Shape apperature = new Ellipse() { Height = appDiameter, Width = appDiameter };
            RadialGradientBrush appBrush = new RadialGradientBrush();
            appBrush.GradientStops.Add(new GradientStop(boatColor, 0.250));
            appBrush.GradientStops.Add(new GradientStop(boatColor, 0.100));
            appBrush.GradientStops.Add(new GradientStop(boatColor, 8));
            apperature.Fill = appBrush;
            Canvas.SetRight(apperature, loopRight + (loopDiameter - appDiameter) / 2);
            Canvas.SetTop(apperature, loopTop + (loopDiameter - appDiameter) / 2);
            canvasArea.Children.Add(apperature);
        }

        //erase the loop
        private void removeLoop()
        {
            Shape eraser = new System.Windows.Shapes.Rectangle() { Fill = BoatColor, Height = 60, Width = 60, RadiusX = 0, RadiusY = 0 };
            Canvas.SetRight(eraser, loopRight);
            Canvas.SetTop(eraser, loopTop);
            canvasArea.Children.Add(eraser);
        }

        // draw either stick as consumable or grid stick -- GOOD
        private void drawSticks()
        {
            int stickHeight = gridStickWidth;
            if (gridPlacementConfig == staggered) stickHeight = (int)(1.5 * gridStickWidth);
            for (int i = 0; i < numberOfSticks; i++)
            {
                Shape stick = new System.Windows.Shapes.Rectangle() { Fill = StickColor, Height = stickHeight, Width = stickWidth, RadiusX = 12, RadiusY = 12 };
                Canvas.SetLeft(stick, stickLeft);
                Canvas.SetTop(stick, i * (stickHeight + 5) + stickTop);
                canvasArea.Children.Add(stick);
                if (gridPlacementConfig != staggered) drawGridsForGridSticks(i * 50 + 5);
                else drawGridsForConsSticks();
            }
        }

        // draw the grids on the grid sticks
        private void drawGridsForGridSticks(int top)
        {
            double xCanvasPosition, yCanvasPosition;
            for (int i = 0; i < numberOfGridsPerStick; i++)
            {
                xCanvasPosition = 10 + i * 45;
                yCanvasPosition = top + 5;
                Shape grid = gridMaker(gridColor);

                Canvas.SetLeft(grid, xCanvasPosition);
                Canvas.SetTop(grid, yCanvasPosition);
                canvasArea.Children.Add(grid);
            }
        }

        // draw the grids on the consumable sticks
        private void drawGridsForConsSticks()
        {
            double colPosition, colNumber, virtColNum;
            int stickHeight = (int)(1.5 * gridStickWidth);
            for (int i = 0; i < totalNumberOfGrids; i++)
            {
                double xCanvasPosition = 20 + gridStickWidth / 2;
                double yCanvasPosition = 15;
                colPosition = i % numberOfSticks;
                colNumber = i / numberOfSticks;
                virtColNum = colNumber;
                if (colNumber >= (numberOfGridsPerStick / 2))
                {
                    xCanvasPosition -= (double)(1.5 * gridStickWidth / 2);
                    yCanvasPosition += (double)(gridStickWidth / 2);
                    virtColNum -= numberOfGridsPerStick / 2;
                }
                xCanvasPosition += 1.5 * virtColNum * gridStickWidth;
                yCanvasPosition += colPosition * (stickHeight + 5) - 5;
                Shape grid = gridMaker(gridColor);
                Canvas.SetLeft(grid, xCanvasPosition);
                Canvas.SetTop(grid, yCanvasPosition);
                canvasArea.Children.Add(grid);
                labelGrid(xCanvasPosition + 10, yCanvasPosition + 5, (i + sectionStartingNo).ToString(), Colors.Black);
            }
        }

        // makes a grid shape
        public Shape gridMaker(System.Windows.Media.Color color)
        {
            Shape grid = new Ellipse() { Height = gridDiameter, Width = gridDiameter };
            RadialGradientBrush brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(color, 0.250));
            brush.GradientStops.Add(new GradientStop(color, 0.100));
            brush.GradientStops.Add(new GradientStop(color, 8));
            grid.Fill = brush;
            return grid;
        }

        //draw the staggered dimension
        private void drawStaggeredDimensions(double x1, double x2, double y1)
        {
            Line stagger = new Line();
            stagger.X1 = x1;
            stagger.X2 = x1;
            stagger.Y1 = y1;
            stagger.Y2 = 25;

            Line stagInd = new Line();
            stagInd.X1 = x1;
            stagInd.X2 = x1 + 30;
            stagInd.Y1 = 25;
            stagInd.Y2 = 25;
            SolidColorBrush redBrush = new SolidColorBrush();
            redBrush.Color = Colors.DarkRed;
            stagger.StrokeThickness = 4;
            stagger.Stroke = redBrush;
            stagInd.StrokeThickness = 4;
            stagInd.Stroke = redBrush;
            stagInd.StrokeDashArray = new DoubleCollection() { 1 };
            canvasArea.Children.Add(stagger);
            canvasArea.Children.Add(stagInd);
            labelGrid((x2 - x1) / 2 + x1 / 2, y1 / 2, (yGridStaggerDisplacement.ToString() + "mm"), Colors.DarkRed);
        }

        //draw the distances between the grids in x and y
        private void drawStickDistLines()
        {
            double x1 = 28;
            double y1 = 24;
            double x2 = 74;
            double y2 = 74;
            if (gridPlacementConfig == staggered)
            {
                x1 = 28;
                y1 = 50;
                x2 = 90;
                y2 = 122;
                drawStaggeredDimensions(x1, x2, y1);
            }
            Line xLine = new Line();
            xLine.X1 = x1;
            xLine.X2 = x2;
            xLine.Y1 = y1;
            xLine.Y2 = y1;
            Line yLine = new Line();
            yLine.X1 = x1;
            yLine.X2 = x1;
            yLine.Y1 = y1;
            yLine.Y2 = y2;
            SolidColorBrush greenBrush = new SolidColorBrush();
            greenBrush.Color = Colors.DarkGreen;
            SolidColorBrush blueBrush = new SolidColorBrush();
            blueBrush.Color = Colors.DarkBlue;
            xLine.StrokeThickness = 4;
            xLine.Stroke = greenBrush;
            yLine.StrokeThickness = 4;
            yLine.Stroke = blueBrush;
            canvasArea.Children.Add(xLine);
            canvasArea.Children.Add(yLine);
            labelGrid((x2 - x1) / 2 + x1 / 2, y1, (xGridDisplacement.ToString() + "mm"), Colors.DarkGreen);
            labelGrid((x2 - x1) / 2 + x1 / 2, y2 - 10, (yGridDisplacement.ToString() + "mm"), Colors.DarkBlue);
        }

        //label which section will go where for grid sticks, if the cassette is ideal
        private void labelGridSticks()
        {
            double xGridPlacement = 0;
            double yGridPlacement = 0;
            for (int i = 0; i < totalNumberOfGrids; i++)
            {
                if (gridPlacementConfig == rowsColConfig)
                {
                    xGridPlacement = 18 + (i % numberOfGridsPerStick) * 45;
                    yGridPlacement = 18 + (i / numberOfGridsPerStick) * 50;
                }
                if (gridPlacementConfig == colsRowConfig)
                {
                    xGridPlacement = 18 + (i / numberOfSticks) * 45;
                    yGridPlacement = 18 + (i % numberOfSticks) * 50;
                }
                labelGrid(xGridPlacement, yGridPlacement, (i + sectionStartingNo).ToString(), Colors.Black);
            }
        }

        // label the grids
        private void labelGrid(double x, double y, string text, System.Windows.Media.Color color)
        {
            TextBlock gridText = new TextBlock();
            gridText.Text = text;
            gridText.Foreground = new SolidColorBrush(color);
            Canvas.SetLeft(gridText, x);
            Canvas.SetTop(gridText, y);
            canvasArea.Children.Add(gridText);
        }

        //
        private double[] getCanvasPositionForCassettePositionNumber(int cassPosNo)
        {
            double[] position = new double[2];
            switch (gridPlacementConfig)
            {
                case rowsColConfig:
                    position[0] = 10 + (cassPosNo % numberOfGridsPerStick) * 45; //+18
                    position[1] = 10 + (cassPosNo / numberOfGridsPerStick) * 50; //+18
                    break;
                case colsRowConfig:
                    position[0] = 10 + (cassPosNo / numberOfSticks) * 45;
                    position[1] = 10 + (cassPosNo % numberOfSticks) * 50;
                    break;
                case staggered:
                    double colPosition, colNumber, virtColNum;
                    int stickHeight = (int)(1.5 * gridStickWidth);
                    position[0] = 20 + gridStickWidth / 2;
                    position[1] = 15;
                    colPosition = cassPosNo % numberOfSticks;
                    colNumber = cassPosNo / numberOfSticks;
                    virtColNum = colNumber;
                    if (colNumber >= (numberOfGridsPerStick / 2))
                    {
                        position[0] -= (double)(1.5 * gridStickWidth / 2);
                        position[1] += (double)(gridStickWidth / 2);
                        virtColNum -= numberOfGridsPerStick / 2;
                    }
                    position[0] += 1.5 * virtColNum * gridStickWidth;
                    position[1] += colPosition * (stickHeight + 5) - 5;
                    break;
            }
            return position;
        }

        private void colorGridAtPositionXY(System.Windows.Media.Color color, double x, double y)
        {
            Shape grid = gridMaker(color);
            Canvas.SetLeft(grid, x);
            Canvas.SetTop(grid, y);
            canvasArea.Children.Add(grid);
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
            bool isReadyForNextState = false;
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
            // Classic Buttons and Accounting
            if (bReady && xReady && yReady && m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Start)) State = PICKING_UP_LOOP_STATE; //Initialization is finished
            if (State > INITIALIZATION_STATE && !m_xboxState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back) && (LeftCurrentStateDeadZone && RightCurrentStateDeadZone)) isReadyForNextState = true;
            if (buttonPressed(GamepadButtonFlags.A)) OnXBoxGamepadButtonPressA.Invoke(this, null);
            if (buttonPressed(GamepadButtonFlags.B) && isReadyForNextState) OnXBoxGamepadButtonPressB.Invoke(this, null);
            if (buttonPressed(GamepadButtonFlags.X) && isReadyForNextState) OnXBoxGamepadButtonPressX.Invoke(this, null);
            if (buttonPressed(GamepadButtonFlags.Y) && isReadyForNextState) OnXBoxGamepadButtonPressY.Invoke(this, null);

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
                //assign the int array then the string from it
                BButtonPosition = zaberGetCurrentPos();
                bButtonPosition = "(" + Math.Round(stepsToMM("x", BButtonPosition[0]), 3).ToString() + ", " + Math.Round(stepsToMM("y", BButtonPosition[1]), 3).ToString() + ", " + Math.Round(stepsToMM("z", BButtonPosition[2]), 3).ToString() + ")";
                BBox.Text = bButtonPosition;
                cassettePositionNumber = 0;
                bInitialization.IsChecked = true;
                bReady = true;
                updateCassette();
                hideGridParameters();
                showDataAndCassette();
                drawBoat();
                initializationPrompt("x");
            }
            if (SimultaneousBackandXCur && !SimultaneousBackandXLast)
            {
                XButtonPosition = zaberGetCurrentPos(); 
                xButtonPosition = "(" + Math.Round(stepsToMM("x", XButtonPosition[0]), 3).ToString() + ", " + Math.Round(stepsToMM("y", XButtonPosition[1]), 3).ToString() + ", " + Math.Round(stepsToMM("z", XButtonPosition[2]), 3).ToString() + ")";
                XBox.Text = xButtonPosition;
                loopPickUpCount = 0;
                xInitialization.IsChecked = true;
                xReady = true;
                updateCassette();
                hideGridParameters();
                showDataAndCassette();
                drawBoat();
                initializationPrompt("y");
            }
            if (SimultaneousBackandYCur && !SimultaneousBackandYLast)
            {
                YButtonPosition = zaberGetCurrentPos();
                yButtonPosition = "(" + Math.Round(stepsToMM("x", YButtonPosition[0]), 3).ToString() + ", " + Math.Round(stepsToMM("y", YButtonPosition[1]), 3).ToString() + ", " + Math.Round(stepsToMM("z", YButtonPosition[2]), 3).ToString() + ")";
                YBox.Text = yButtonPosition;
                yInitialization.IsChecked = true;
                yReady = true;
            }

            // Combos to decrement button counters
            if (SimultaneousDownDandBCur && !SimultaneousDownDandBLast && State == PLACING_SECTION_STATE)
            {
                MoveToLastDropOffPosition.Invoke(this, null);
            }
            if (SimultaneousDownDandXCur && !SimultaneousDownDandXLast && State == PICKING_UP_LOOP_STATE)
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
            try
            {
                thorDevice.GetSettings(thorDevice.MotorDeviceSettings);
                if (thorDevice.MotorDeviceSettings.Physical.TravelMode == PhysicalSettings.TravelModes.Linear)
                {
                    throw new Exception("Cannot drive a linear motor in continous mode");
                }
                thorDevice.MotorDeviceSettings.Rotation.RotationMode = RotationSettings.RotationModes.RotationalUnlimited;
                thorDevice.SetSettings(thorDevice.MotorDeviceSettings, true, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            //deviceUnitConverter = thorDevice.UnitConverter; // wtf
            thorDevice.StartPolling(thorPollRate);

            // display info about device     
            DeviceInfo deviceInfo = thorDevice.GetDeviceInfo();
            Console.WriteLine("Device {0} = {1}", deviceInfo.SerialNumber, deviceInfo.Name);

        }

        public void thorMove(IGenericAdvancedMotor device, MotorDirection direction)
        {
            
            try
            {
                device.MoveContinuous(direction);
                //device.MoveRelative(direction, thorDisplacement, 100);
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
            //thorDevice.StopImmediate();
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
            string beginningDataStreamMarker = "";
            if (Arduino.IsOpen)
            {
                beginningDataStreamMarker = Arduino.ReadTo("a");
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
        // to the future position(futPos). 
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
            
            switch (gridPlacementConfig)
            {
                case rowsColConfig:
                    xDisplacementFromOrigin = (cassettePositionNumber % numberOfGridsPerStick) * mmToSteps("x", xGridDisplacement);
                    yDisplacementFromOrigin = (cassettePositionNumber / numberOfGridsPerStick) * mmToSteps("y", yGridDisplacement);
                    break;
                case colsRowConfig:
                    xDisplacementFromOrigin = (cassettePositionNumber / numberOfSticks) * mmToSteps("x", xGridDisplacement);
                    yDisplacementFromOrigin = (cassettePositionNumber % numberOfSticks) * mmToSteps("y", yGridDisplacement);
                    break;
                case staggered:
                    int colPosition, colNumber;
                    colPosition = cassettePositionNumber % numberOfSticks;
                    colNumber = cassettePositionNumber / numberOfSticks;
                    int virtColNum = colNumber;
                    if (colNumber >= numberOfCols)
                    {
                        xDisplacementFromOrigin -= mmToSteps("x", xGridDisplacement / 2);
                        yDisplacementFromOrigin += mmToSteps("y", yGridStaggerDisplacement);
                        virtColNum -= numberOfCols;
                    }
                    xDisplacementFromOrigin += virtColNum * mmToSteps("x", xGridDisplacement);
                    yDisplacementFromOrigin += colPosition * mmToSteps("y", yGridDisplacement);
                    break;
            }
            futPos[0] = xDisplacementFromOrigin + BButtonPosition[0];
            futPos[1] = yDisplacementFromOrigin + BButtonPosition[1];
            futPos[2] = BButtonPosition[2];
            zaberMoveStoredPositionAllAtOnce(futPos);
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

# JoystickProject #

The purpose of this project is to decrease the time taken in ultramicrotome sectioning by increasing automation of the process.  In our setup, we have an Arduino, 3 Zaber actuators, and 1 Thorlabs actuator.  In the first version of this project, the Arduino controls a solenoid valve that acts as a binary blower(off/on), and reads from sensors to get environment information.  The Zaber actuators are daisy chained to make a pair of tweezers move in x,y,z directions, while the Thorlabs actuator is used to open and close the tweezers for grabbing purposes.

##XBoxStage##
I took code written by @jaybo and modified it so that I have an XBox controller controlling my Arduino (Uno on COM5 at baud = 9600), Zaber actuators(on COM6, baudrate = 9600), and Thorlabs actuator controlled by a Thorlabs KCube DCServo.  I use a WPF in Visual Studio C#.  This project depends on SharpDX.XInput, Thorlabs.MotionControl.DeviceManager, Thorlabs.MotionControl.GenericMotor, Thorlabs.MotionControl.KCube.DCServo (http://www.thorlabs.us/Software/Motion%20Control/KINESIS/Application/KINESIS%20Install%20x64/setup.exe), Zaber, Zaber.Serial.Core (http://zaber.com/software/zaber-core-serial-csharp-v1.2.zip), and Live-Charts (@beto-rodriguez https://github.com/beto-rodriguez/Live-Charts).

The GUI guides you through your grid setup by allowing you to choose which configuration of loop drop off you'd like, giving you n x m options for a staggered configuration, row configuration, or vertical configuration.  Once you've chosen the configuration that you'd like, you need to save the initial positions for the B (loop pickup), X (loop dropoff), Y (section pickup) buttons. 

###Arduino Component Setup###
Implementing Live-Chart, I've been able to add live feedback from the Arduino sensors Rev C(wind and temperature: https://moderndevice.com/product/wind-sensor/) and a Hall Effect Probe (a magnet on the microtome helps establish its cutting rate: https://moderndevice.com/product/a1324-hall-effect-sensor/).  I'm currently using a solenoid from McMaster-Carr to allow pressurized air to blow sections off the cutting block and into the boat(http://www.mcmaster.com/#5001t37/=14ftjrt).  This will be replaced by a proportional air blower when the parts come in.  For the proportional blower, I'll be using a proportional air blower with a similar configuration to Strey lab's Arduino setup(http://streylab.com/blog/2015/4/8/open-hardware-microfluidics-controller-arduino-shield).

WindHallAirTempV2.0 is the Arduino .ino file that controls the Air puffer (Wind) reads from Hall Effect sensor, Air current, and Temperature.

###Thorlabs Component Setup###
KCube DC Servo controls a Thorlabs actuator.  This component will be replaced in a future version as it does *not* have the functionality advertised for the product available in for C# WPF applications.

###Zaber Component Setup###
Currently, there is a function for attempting to avoid no-fly zones(areas where our actuators will run into other equipment).  If the equipment can move along each axis sequentially, for a total of 3 moves or less, to another valid position, it will do so.  This function does *not* move the equipment to the opposite side of obstacles.  The function tries each permutation of moves in order to determine the move sequence. Each of the obstacles needs to be hardcoded in as a rectangular NoFlyZone structure.  I'm not currently implementing it because there's little motion in the Z direction, and a limited range that my save buttons go between.

The save position buttons need to be initialized.  You do this by hitting the XBox gamepad's "Back" button + B, X, or Y.  B is designated at the ""  Hitting these buttons before initialization currently homes the Zaber actuators. 

###Software Description###
####Finite State Machine#### 
Implementing a finite state machine allows for automated record keeping, and increased automation of loop drop off and pick up locations.  To begin the process,  the Initialization State (state 0), requires the Position Recall buttons to be initialized.  To move to the next state, the "Start" button needs to be pressed on the XBox controller.  

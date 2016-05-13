# JoystickProject
*****This project is in progress***** 

This repo has a few subprojects within it.  The purpose of this project is to decrease the time taken in ultramicrotome sectioning by increasing automation of the process.  In our setup, we have an Arduino, 3 Zaber actuators, and 1 Thorlabs actuator.  At this time, the Arduino controls a blower, but will probably control more things in the future.  The Zaber actuators are daisy chained to make a pair of tweezers move in x,y,z directions, while the Thorlabs actuator will be used to open and close the tweezers for grabbing purposes.

**"Instructable Example"**
I used an example to figure out how to get a Windows Forms C# project to talk to my Arduino to turn on an LED.  No big deal.

**"ConsoleZaberCommanderASCII"**
I built and tested some code to deal with "noFlyZones" or places I don't want our expensive equipment to run into.  It *should* avoid these zones, and from the tests I've run, it does, with the exception of moving to the opposite side of an obstacle.  I haven't built that part of the code because I think it will be outside the scope of this entire project.  Our zones are fairly large objects that can be approximated with a few rectangular areas.

**"XBoxStage"**
I took code written by @jaybo and modified it so that I have an XBox controller controlling an Arduino (Uno on COM5 at baud = 9600), 3 Zaber actuators(on COM6, baudrate = 9600), and 1 Thorlabs actuator controlled by a KCube DCServo.  I use a WPF in Visual Studio C#.  This project depends on SharpDX.XInput, Thorlabs.MotionControl.DeviceManager, Thorlabs.MotionControl.GenericMotor, Thorlabs.MotionControl.KCube.DCServo(http://www.thorlabs.us/Software/Motion%20Control/KINESIS/Application/KINESIS%20Install%20x64/setup.exe), Zaber, Zaber.Serial.Core(http://zaber.com/software/zaber-core-serial-csharp-v1.2.zip).  

This project uses a timer to poll the gamepad.(I had more success with this method then threading.  I might try a threading based approach again, but may not have time to with this project...)

The joysticks map to an exponential function when outside of the deadzone for their respective joystick.  This function is (e^(a*x)-1)/e^a where a = joystickVelocityModulator.  Experience has shown that a value of "1.2" works well for our purposes.  Fortunately, because the variable is close to 1, the difference in "diagonal velocity" versus "along-axis velocity" isn't as differentiable ;) 

Currently, there is a function for attempting to avoid no-fly zones(areas where our actuators will run into other equipment).  If the equipment can move along each axis sequentially, for a total of 3 moves or less, to another valid position, it will do so.  This function does *not* move the equipment to the opposite side of obstacles.  The function tries each permutation of moves in order to determine the move sequence. Each of the obstacles needs to be hardcoded in as a rectangular NoFlyZone structure. 

In the future, I will fix the "save position" buttons for the Zaber actuators so that a command can be sent to move the actuators.  (In the farther future, I hope to add a finite state machine to toggle between different parts of the process of picking up a section and placing it on the grid.  Then maybe a counter can be implemented, such that it picks up loops sequentially...  Then maybe some machine vision so the robot can do the entire process without supervision...)

***"HOTAS" - To Come***
Eventually, I'll do some work to utilize code written by @mdjarv to use a HOTAS Warthog joystick to control 
the series of 3 Zaber actuators, 1 Thorlabs Actuator, and Arduino.  

*****This project is in progress*****

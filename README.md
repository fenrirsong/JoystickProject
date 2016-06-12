# JoystickProject
*****This project is in progress***** 

This repo has a few subprojects within it.  The purpose of this project is to decrease the time taken in ultramicrotome sectioning by increasing automation of the process.  In our setup, we have an Arduino, 3 Zaber actuators, and 1 Thorlabs actuator.  At this time, the Arduino controls a blower, but will probably control more things in the future.  The Zaber actuators are daisy chained to make a pair of tweezers move in x,y,z directions, while the Thorlabs actuator will be used to open and close the tweezers for grabbing purposes.

**"Instructable Example"**
I used an example to figure out how to get a Windows Forms C# project to talk to my Arduino to turn on an LED.  No big deal.

**"XBoxStage"**
I took code written by @jaybo and modified it so that I have an XBox controller controlling an Arduino (Uno on COM5 at baud = 9600), 3 Zaber actuators(on COM6, baudrate = 9600), and 1 Thorlabs actuator controlled by a KCube DCServo.  I use a WPF in Visual Studio C#.  This project depends on SharpDX.XInput, Thorlabs.MotionControl.DeviceManager, Thorlabs.MotionControl.GenericMotor, Thorlabs.MotionControl.KCube.DCServo(http://www.thorlabs.us/Software/Motion%20Control/KINESIS/Application/KINESIS%20Install%20x64/setup.exe), Zaber, Zaber.Serial.Core(http://zaber.com/software/zaber-core-serial-csharp-v1.2.zip).  

This project uses a timer to poll the gamepad.(I had more success with this method then threading.  I might try a threading based approach again, but may not have time to with this project...)

The joysticks map to an exponential function when outside of the deadzone for their respective joystick.  This function is (e^(a*x)-1)/e^a where a = joystickVelocityModulator.  Experience has shown that a value of "1.2" works well for our purposes.  

Currently, there is a function for attempting to avoid no-fly zones(areas where our actuators will run into other equipment).  If the equipment can move along each axis sequentially, for a total of 3 moves or less, to another valid position, it will do so.  This function does *not* move the equipment to the opposite side of obstacles.  The function tries each permutation of moves in order to determine the move sequence. Each of the obstacles needs to be hardcoded in as a rectangular NoFlyZone structure.  I'm not currently implementing it because there's little motion in the Z direction, and a limited range that my save buttons go between... speaking of...

The save position buttons need to be initialized.  You do this by hitting DPad Up + B, X, or Y.  Hitting these buttons before initialization currently homes the Zaber actuators.  I'll initialize them to safer values in the near future.

Implementing Live-Chart, I've been able to add live feedback from the Arduino sensors Rev C(wind and temperature: https://moderndevice.com/product/wind-sensor/) and a Hall Effect Probe (a magnet on the microtome helps establish its cutting rate: https://moderndevice.com/product/a1324-hall-effect-sensor/).  I still need to work a bit on the Arduino end of things to get my power FETs to modulate air flow to blow sections off the block... but there's some code that *should* be sending bytes to my Arduino for PWM.

In the farther future, I hope to add a finite state machine to toggle between different parts of the process of picking up a section and placing it on the grid.  Then maybe a counter can be implemented, such that it picks up loops sequentially...  Then maybe some machine vision so the robot can do the entire process without supervision...  More to come!

***"HOTAS" - To Come***
Eventually, I'll do some work to utilize code written by @mdjarv to use a HOTAS Warthog joystick to control 
the series of 3 Zaber actuators, 1 Thorlabs Actuator, and Arduino.  

*****This project is in progress*****

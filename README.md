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
####Getting Up and Running
Assuming that you're running Windows, C\# works pretty well.

*Connecting the Zaber and Arduino in the software*    My Zaber port is on COM5. My Arduino port is on COM6.  You can go ahead and plug you Zaber devices in a daisy chain, and into the computer, and find with COM port your actuators are on by going to Control Panel -> Device Manager -> Ports (COM & LPT).  It should be listed there.  You'll need to make sure that your COM port for the Zaber and Arduino match what's in the code.

*Loading Arduino sketch*    Be sure to load the Arduino sketch, WindHallAirTempV2.0.ino to your Arduino board, with the appropriate pins connected to your sensors and solenoid.  To make sure everything is reading correctly, open the Serial Monitor under the Tools menu in the Arduino IDE.  You should see 3 different numbers separated by semicolons (';') and an 'a' every 3rd number.  This is what is read in by the C\# portion of the code to make the Live-Charts you will see while sectioning.  Enter 'a' to tell the Arduino to allow current to flow to the solenoid.  If this works, then your Arduino should be correctly setup.

*Connecting the Thorlabs in software*    Take a look on the back of your KCube DC servo.  There should be a serial number there.  To make the code specific to your servo, change the line of code in the beginning "puclib string serialNo = "27000117"" to whatever your serialNo is.

####Finite State Machine and Operational Use
Implementing a finite state machine allows for automated record keeping, and increased automation of loop drop off and pick up locations.  

*Starting "Initialization State"*:    To begin the process,  the "Initialization State" (state 0), requires the position recall buttons to be initialized.  To move to the next state, the "Start" button needs to be pressed on the XBox controller.  

*Going to "Pick up a Loop State"*:  Pressing "Start" after initialization of the position recall buttons bring you to the "Picking up Loop State" (state 1).  I've written my code such that it expects columns of 10 loops, with handles pointing to the left, arranged in a staggered position, such that the one furthest and left from the operator is the 0th position.  To see this refer to a figure coming to you soon *Insert Figure here*.

*Staying in the "Pick up Loop State"*:  Sometimes advancing to the next loop is necessary because loop pick up doesn't always go exactly as planned.  Maybe the handle got oriented a different direction while you were trying to pick it up.  No worries, hit the "X" Button again, and it'll take you to the next loop.  Sometimes you accidentally advance past a perfectly good loop.  Again, don't worry, press the D-Pad's "Down" button and the "X" button simultaneously and it will bring you back to the last loop.

*Going to "Picking up Section State"*:  After picking up a loop, you will now want to pick up a section.  Pressing the "Y" button will bring you to the "Picking up Section State" (state 2).  Here, the actuators bring you to the boat where the section is floating.  Use the right joystick to lower the loop into the water surrounding the section.  Use the right joystick again to move the loop and section out of the water.

*Staying in the "Pick up Section State"*:   On occasion, the section will drop through the center of the loop and will not be picked up by the loop.  You shouldn't be too far from the boat, however, you may press the "Y" button again to move the loop to your initialized boat position again.

*Returning to the "Pick up Loop State"*:   Accidents happen.  Perhaps the loop wasn't picked up.  This will return you to the relevant loop pick up site so you can try again to pick up your loop.

*Going to "Placing Section State"*:   Pressing the "B" button brings you to what is assumed to be the next drop off location for the serial section.  

*Staying in "Placing Section State"*: In the case of a broken film, the "B" button can be pressed again to move onto the next grid drop off location.  Moving to the next drop off location records that there was a broken film, or some other problem with that particular drop off location.  Accidental advancement to the next grid location is reversible.  Pressing the D-Pad's "Down" button simultaneously with the "B" button will bring the position to the previous grid location.  Combining these two buttons will also be recorded, and the counter for the location will be edited to match the correct location and section number.

*Going to "Picking up Loop State"*:   Great!  You've successfully placed a section down on your grid and you're ready to pick up more.  Press the "X" button to go to your next loop to pick up.  During this step, the recording of which position which section is at will be recorded to a file associated with the current time and date.  Once again, you'll have the ability to advance or to back up to the previous loop position.

*"Going to "Picking up Loop State" at End of Cassette*:    After you've cycled through all the positions for your particular setup, the GUI will ask you if you want to continue to the next cassette.  It assumes that the next cassette will have the same configuration as your current one.  Regardless of what you select, it will record to file a picture of your recently finished cassette.  If you select "No" at this point, it will exit the program.

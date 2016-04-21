# JoystickProject
*****This project is in progress***** 

This repo has a few subprojects within it.  The purpose of this project is to decrease the time taken in ultramicrotome sectioning by increasing automation of the process.  In our setup, we have an Arduino, 3 Zaber actuators, and 1 Thorlabs actuator.  At this time, the Arduino controls a blower, but will probably control more things in the future.  The Zaber actuators are daisy chained to make a pair of tweezers move in x,y,z directions, while the Thorlabs actuator will be used to open and close the tweezers for grabbing purposes.

In "Instructable Example" I used an example to figure out how to get a Windows Forms C# project to talk to my Arduino to turn on an LED.  No big deal.

In "XBoxStage-master", I have taken code written by @jaybo and modified it so that I have an XBox controller controlling an Arduino (Uno on COM5 at baud = 9600) using Visual Studio C#, WPF, and SharpDX.XInput.  In the future, I will add control over 3 Zaber actuators, and 1 Thorlabs actuator.  In the farther future, I will add a finite state machine to toggle between different parts of the process of picking up a section and placing it on the grid.

Eventually, I'll do some work to utilize code written by @mdjarv to use a HOTAS Warthog joystick to control 
the series of 3 Zaber actuators, 1 Thorlabs Actuator, and Arduino.  

*****This project is in progress*****

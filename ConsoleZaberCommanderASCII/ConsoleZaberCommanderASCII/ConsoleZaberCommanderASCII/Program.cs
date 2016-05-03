using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zaber;
using Zaber.PlugIns;
using Zaber.Serial.Core;

namespace ConsoleZaberCommanderASCII
{
    class Program
    {
        static public int X_MAX = 1526940;
        static public int Y_MAX = 3149606;
        static public int Z_MAX = 305381;
        int mmToSteps(string axis, double mm)
        {
            int steps = 0;
            double stepsPerMM = 0;

            switch (axis)
            {
                // X Axis is a X-LSQ150A Zaber Actuator
                case "axisX":
                    stepsPerMM = 10078.74;
                    break;
                // Y Axis is a X-LRM150A Zaber Actuator
                case "axisY":
                    stepsPerMM = 20997.375;
                    break;
                // Z Axis is a X-LSQ150B Zaber Actuator
                case "axisZ":
                    stepsPerMM = 2015.74803;
                    break;
                default:
                    Console.WriteLine("Axis selected does not exist");
                    return 0;
            }

            steps = Convert.ToInt32(stepsPerMM * mm);
            return steps;
        }

        // Structure for the deadzones, units in steps
        struct deadzone
        {
            public int xAxisMin, xAxisMax;
            public int yAxisMin, yAxisMax;
            public int zAxisMin, zAxisMax;
            public deadzone(int xmin, int xmax, int ymin, int ymax, int zmin, int zmax)
            {
                xAxisMin = xmin;
                xAxisMax = xmax;
                yAxisMin = ymin;
                yAxisMax = ymax;
                zAxisMin = zmin;
                zAxisMax = zmax;
            }
        }

        // Define the parameters of the different deadzones
        static deadzone[] deadZoneArray = new deadzone[3]
        {
            // fill out these deadzones after testing...
            // This deadzone is the microtome's plastic part in steps
            new deadzone(734320, X_MAX, 0, Y_MAX, 271423, Z_MAX),
            // This deadzone is the microtome from approx the boat up 
            // NEED TO MAP DEADZONE WITH SETUP COMPLETED!!!
            new deadzone(1216976, X_MAX, 0, 1700412, 271423, Z_MAX),
            // This deadzone is the block region of the microtome
            new deadzone(1080153, X_MAX, 0, 499378, 0, Z_MAX)
        };

        // Check position so it doesn't run into anything
        static public bool isDeadZone(int[] Pos)
        {
            bool isIt = true;
            int tracker = 0;

            //for all deadzones, check if position is within deadzone range
            foreach (deadzone myDeadZone in deadZoneArray)
            tracker += Pos[0] > myDeadZone.xAxisMin ? (Pos[0] < myDeadZone.xAxisMax ?
                (Pos[1] > myDeadZone.yAxisMin ? (Pos[1] < myDeadZone.yAxisMax ?
                (Pos[2] > myDeadZone.zAxisMin ? (Pos[2] < myDeadZone.zAxisMax ?
                1 : 0) : 0) : 0) : 0) : 0) : 0;

            if (tracker == 0) isIt = false;
            if (isIt == true) Console.Beep();
            Console.WriteLine("Trying to move into a dead zone!");

            return isIt;
        }

        // This function avoids deadzones by finding an order of operations for
        // axis moves.  
        // SUPER IMPORTANT NOTE: If there is a deadzone between two freezones, 
        // it may run into something!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // Make sure that tweezer tips don't "Drop" between physical objects 
        // without handling!!!!!
        public static int[] avoidDeadZones(int[] futPos, int[] curPos)
        {
            int[] tryPos1 = new int[] {futPos[0], curPos[1], curPos[2]};
            int[] tryPos2 = new int[] { 0, 0, 0 };
            int[] moveSequence = { -1, -1, -1 };
            // try xyz and xzy sequence
            if (!isDeadZone(tryPos1))
            {
                Array.Copy(futPos, tryPos2, -1);
                tryPos2[2] = curPos[2];
                if (!isDeadZone(tryPos2))
                {
                    Console.WriteLine("xyz should work");
                    moveSequence[0] = 0;
                    moveSequence[1] = 1;
                    moveSequence[2] = 2;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, -1);
                    tryPos2[1] = curPos[1];
                    if (!isDeadZone(tryPos2))
                    {
                        Console.WriteLine("xzy should work");
                        moveSequence[0] = 0;
                        moveSequence[1] = 2;
                        moveSequence[2] = 1;
                    }
                }
            }

            // try yxz and yzx sequence
            Array.Copy(curPos, tryPos1, -1);
            tryPos1[1] = futPos[1];
            if (!isDeadZone(tryPos1))
            {
                Array.Copy(futPos, tryPos2, -1);
                tryPos2[2] = curPos[2];
                if (!isDeadZone(tryPos2))
                {
                    Console.WriteLine("yxz should work");
                    moveSequence[0] = 1;
                    moveSequence[1] = 0;
                    moveSequence[2] = 2;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, -1);
                    tryPos2[0] = curPos[0];
                    if (!isDeadZone(tryPos2))
                    {
                        Console.WriteLine("yzx should work");
                        moveSequence[0] = 1;
                        moveSequence[1] = 2;
                        moveSequence[2] = 0;
                    }
                }
            }

            // try zxy and zyx sequence
            Array.Copy(curPos, tryPos1, -1);
            tryPos1[2] = futPos[2];
            if (!isDeadZone(tryPos1))
            {
                Array.Copy(futPos, tryPos2, -1);
                tryPos2[1] = curPos[1];
                if (!isDeadZone(tryPos2))
                {
                    Console.WriteLine("zxy should work");
                    moveSequence[0] = 2;
                    moveSequence[1] = 0;
                    moveSequence[2] = 1;
                }
                else
                {
                    Array.Copy(futPos, tryPos2, -1);
                    tryPos2[0] = curPos[0];
                    if (!isDeadZone(tryPos2))
                    {
                        Console.WriteLine("zyx should work");
                        moveSequence[0] = 2;
                        moveSequence[1] = 1;
                        moveSequence[2] = 0;
                    }
                }
            }
            //For the future -- if needing to navigate around different things
            //add in bisection algorithm that checks to see if half-way paths 
            //can meet requirements of avoiding deadzones.

            return moveSequence;
        }

        static void goodbye(string thing)
        {
            Console.WriteLine("Int32.TryParse could not parse '{0}' to an int.\n", thing);
            Console.WriteLine("Well, that's too bad!  Goodbye!");
            System.Threading.Thread.Sleep(50000);
        }

        static void Main(string[] args)
        {
            //ZaberAsciiPort is part of the Zaber.Serial.Core.dll lib
            var port = new ZaberAsciiPort("COM5");
            port.Open();
            Console.WriteLine("Port has been opened");
            System.Threading.Thread.Sleep(2000);

            // Device 1 is the joystick.  The devices are then in order of how they
            // are daisy chained
            var axisX = new ZaberAsciiAxis(port, 2, 1); // device 2, x axis
            var axisY = new ZaberAsciiAxis(port, 3, 1); // device 3, y axis
            var axisZ = new ZaberAsciiAxis(port, 4, 1); // device 4, z axis

            // curPos is an array of current (x,y,z) positions
            int[] curPos = new int[3] {axisX.GetPosition(), axisY.GetPosition(), axisZ.GetPosition()};
            // futPos is an array of future (x,y,z) positions
            int[] futPos = new int[3];
            // tryPos is an array of positions that will be converted from the terminal
            string[] tryPos = new string[3] {"", "", ""};
            // 0th element is first axis move, 1st element 2nd axis move, 3rd element last 
            int[] moveSequence = new int[3];
            
            bool parsed = false;
            bool isItDeadZone = false;

            Console.WriteLine("Axises have been assigned");
            System.Threading.Thread.Sleep(2000);

            //Read device positions
            Console.WriteLine("The X axis is at \"{0}\" microsteps", 
                Convert.ToString(axisX.GetPosition()));
            Console.WriteLine("The Y axis is at \"{0}\" microsteps", 
                Convert.ToString(axisY.GetPosition()));
            Console.WriteLine("The Z axis is at \"{0}\" microsteps", 
                Convert.ToString(axisZ.GetPosition()));

            //Ask for user input for new position
            string axis = "";
            for (int i = 0; i < futPos.Length; i++ )
            {
                switch (i)
                {
                    case 0:
                        axis = "x";
                        break;
                    case 1:
                        axis = "y";
                        break;
                    case 2:
                        axis = "z";
                        break;
                }
                Console.WriteLine("Input {0} position you want to try", axis);
                tryPos[i] = Console.ReadLine();
                parsed = Int32.TryParse(tryPos[i], out futPos[i]);
                if (!parsed)
                {
                    goodbye(tryPos[i]);
                    port.Close();
                    Environment.Exit(0);
                }
            }

            // If the inputted range is not within range, take it to min or max
            futPos[0] = futPos[0] < X_MAX ? (futPos[0] >= 0 ? futPos[0] : 0) : X_MAX;
            futPos[1] = futPos[1] < Y_MAX ? (futPos[1] >= 0 ? futPos[1] : 0) : Y_MAX;
            futPos[2] = futPos[2] < Z_MAX ? (futPos[2] >= 0 ? futPos[2] : 0) : Z_MAX;

            // Check to see if inputted range is in a deadzone
            isItDeadZone = isDeadZone(futPos);
            // Only move the axises if it is not in a deadzone
            if (!isItDeadZone)
            {
                moveSequence = avoidDeadZones(futPos, curPos);
                foreach (int element in moveSequence)
                {
                    switch (element)
                    {
                        case 0:
                            axisX.MoveAbsolute(futPos[0]);
                            break;
                        case 1:
                            axisY.MoveAbsolute(futPos[1]);
                            break;
                        case 2:
                            axisZ.MoveAbsolute(futPos[2]);
                            break;
                        case -1:
                            Console.WriteLine("moveSequence Not initialized");
                            System.Threading.Thread.Sleep(3000);
                            goodbye("moveSequence");
                            break;
                    }
                }
                curPos[0] = axisX.GetPosition();
                curPos[1] = axisY.GetPosition();
                curPos[2] = axisZ.GetPosition();
            }

            System.Threading.Thread.Sleep(5000000);

            Console.WriteLine("Closing port...");

            // When closing, it DOES NOT HOME!
            port.Close();
            Console.WriteLine("Shutting down...");
            System.Threading.Thread.Sleep(5000);
            return;
        }
    }
}

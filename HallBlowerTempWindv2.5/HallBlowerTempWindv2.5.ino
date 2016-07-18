
#include "Timer.h"
Timer t;

// Define the stuff for the air blower
#define analogPinForRV    1   // change to pins you the analog pins are using
#define analogPinForTMP   0

const int hallPin = A3;
int hall;

const float zeroWindAdjustment =  1.75; // negative numbers yield smaller wind speeds and vice versa.
int TMP_Therm_ADunits;  //temp termistor value from wind sensor
float RV_Wind_ADunits;    //RV output from wind sensor 
float RV_Wind_Volts;
int TempCtimes100;
float Temp;
float zeroWind_ADunits;
float zeroWind_volts;
float WindSpeed_MPH;
int val = 0;

const int BLOWERPIN = 12; // This is the pin that the blower is conected to
const int BUTTON = 2;  //This is the pin that the button is connected to

void setup() {
  Serial.begin(9600);
  pinMode(BLOWERPIN, OUTPUT);
  pinMode(BUTTON, INPUT);
  t.every(300, takeReading);
}

void loop() {
  t.update();

  if (digitalRead(BUTTON) == HIGH){ 
    digitalWrite(BLOWERPIN, HIGH);
  }
  else {
    digitalWrite(BLOWERPIN, LOW);
  }
}

void takeReading()
{
  TMP_Therm_ADunits = analogRead(analogPinForTMP);
  RV_Wind_ADunits = analogRead(analogPinForRV);
  hall = analogRead(hallPin);
  // these are all derived from regressions from raw data as such they depend on a lot of experimental factors
  // such as accuracy of temp sensors, and voltage at the actual wind sensor, (wire losses) which were unaccouted for.
  TempCtimes100 = (0.005 *((float)TMP_Therm_ADunits * (float)TMP_Therm_ADunits)) - (16.862 * (float)TMP_Therm_ADunits) + 9075.4;  
  Temp = (float)TempCtimes100 / 100;
  Serial.print("a");
  Serial.print(hall);
  Serial.print(";");
  Serial.print(Temp);
  Serial.print(";");
  Serial.print(RV_Wind_ADunits);
  Serial.print(";");
}

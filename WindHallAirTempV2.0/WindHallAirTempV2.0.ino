
#include "Timer.h"
Timer t;

//Define Sensor Stuff
#define TMP_PIN A0 //"A" prefix is for "analog"
#define WIND_PIN A1   
#define HALL_PIN A3
int hall;
const float zeroWindAdjustment =  1.75; // negative numbers yield smaller wind speeds and vice versa.
int TMP_Therm_ADunits;  //temp termistor value from wind sensor
float RV_Wind_ADunits;    //RV output from wind sensor 
int TempCtimes100;
float Temp;
float zeroWind_ADunits;
float zeroWind_volts;
float WindSpeed_MPH;
int val = 0;

//Define Blower
#define AIR_PIN 3

void setup() {
  Serial.begin(9600);
  pinMode(AIR_PIN, OUTPUT);
  t.every(1000, takeReading);
}

void loop() {
  t.update();
  
  if (Serial.available())
  {
    byte trigValue = Serial.parseInt();
    analogWrite(LED, trigValue);  
  }
  else
  {
    analogWrite(LED, 0);  
  }
}

void takeReading()
{
  TMP_Therm_ADunits = analogRead(TMP_PIN);
  RV_Wind_ADunits = analogRead(WIND_PIN);
  hall = analogRead(HALL_PIN);
  TempCtimes100 = (0.005 *((float)TMP_Therm_ADunits * (float)TMP_Therm_ADunits)) - (16.862 * (float)TMP_Therm_ADunits) + 9075.4;  
  Temp = (float)TempCtimes100 / 100;

  Serial.print(hall);
  Serial.print(";");
  Serial.print(Temp);
  Serial.print(";");
  Serial.print(RV_Wind_ADunits);
  Serial.print(";");
}

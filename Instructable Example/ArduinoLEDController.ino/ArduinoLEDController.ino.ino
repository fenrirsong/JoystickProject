int message = 0; // This will hold one byte of the serial message
int LEDPin = 13; // This is the pin that the led is conected to
const int BUTTON=2;

void setup() {
  Serial.begin(9600); //set serial to 9600 baud rate
  pinMode(LEDPin, OUTPUT);
}

void loop(){  
  if (Serial.available() > 0) { // Check to see if there is a new message
    message = Serial.read(); // Put the serial input into the message

    if (message == 'A' ){ // If a capitol A is received
      digitalWrite(LEDPin, HIGH);
      Serial.println("LED on"); // Send back LED on
    }
    if (message == 'a' ){ // If a lowercase a is received
      digitalWrite(LEDPin, LOW);
      Serial.println("LED off"); // Send back LED off
    }
  }
}


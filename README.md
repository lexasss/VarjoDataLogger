# Varjo Data Logger

Logs eye-gaze, head-gaze and hand location into a sinlge file.

## Hardware

Tested with Varjo XR-3 and XR-4 with Leap Motion Controller 2.

## Dependencies

The required binary libraries are included into the repo.

Note that VarjoTrackerLib can be compiled from https://github.com/lexasss/VarjoTrackerLib.git. The compiled DLL file must be copied to this project `libs` folder.

## Notes

The room setup in Varjo Base must be reset once a user takes the upright poistion and faces toward the virtual desktop.
This allows proper headset rotation compensation for hand location data.
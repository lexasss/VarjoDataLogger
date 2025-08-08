# Varjo Data Logger

Logs eye-gaze, head-gaze and hand location into a sinlge file.

## Hardware

Tested with Varjo XR-3 and XR-4 with Leap Motion Controller 2.

## Dependencies

The required binary libraries are included into the repo.

Note that VarjoTrackerLib can be compiled from https://github.com/lexasss/VarjoTrackerLib.git. The compiled DLL file must be copied to this project `libs` folder.

## Command line

```
  -n, --nbtip      IP address of the PC running N-Back task application. Default is '127.0.0.1'.
  -c, --cttip      IP address of the PC running CTT application. Default is '127.0.0.1'.
  -l, --log        Log file folder, must be without spaces. Default is 'C:/Users/<USERNAME>/Documents'.
  -o, --offset     Leap Motion ZYX offsets (comma-separated, no spaces). Default is '-6,15,0'.
  -h, --hide       Forces the console window to be hidden (minimized) while the tracking is on.
  -v, --verbose    Debug info is printed in the verbose mode.
  -d, --debug      Sets to the debug mode.
```

## Notes

The room setup in Varjo Base must be reset once a user takes the upright poistion and faces toward the virtual desktop.
This allows proper headset rotation compensation for hand location data.
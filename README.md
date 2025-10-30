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
  -m, --lmsip      IP address of the PC running Leap Motion Streamer application. Default is '127.0.0.1'.
  -l, --log        Log file folder, must be without spaces. Default is 'C:/Users/<USERNAME>/Documents'.
  -o, --offset     Leap Motion ZYX offsets (comma-separated, no spaces). Default is '-6,15,0'.
  -h, --hide       Forces the console window to be hidden (minimized) while the tracking is on.
  -s, --setup      JSON setup file with study configuration. Default is 'no value'.
  -v, --verbose    Debug info is printed in the verbose mode.
  -d, --debug      Sets to the debug mode.
```

## Notes

The room setup in Varjo Base must be reset once a user takes the upright poistion and faces toward the virtual desktop.
This allows proper headset rotation compensation for hand location data.

## Study configuration

Study setup is specified in a JSON file that has 4 sections

### SessionSetups

This section contains a list of experiment block descriptions, each consisting of 4 parameters:

- Randomized [true/false] - set this parameter to `true` if the trials must be randomized with a block. 
- Repetitions [N>0] - the number of block repetitions (still counted as a single block)
- CttLambdaIndexes [array of int] - indexes of lambdas in CTT application
- NBackTaskIndexes [array of int] - indexes of layouts in NBackTask application

For example, `SessionSetups` may be specified as this:

```
"SessionSetups": [
    {
      "Randomized": false,
      "Repetitions": 1,
      "CttLambdaIndexes": [1, 3],
      "NBackTaskIndexes": [1, 2, 3, 4]
    },
    {
      "Randomized": true,
      "Repetitions": 2,
      "CttLambdaIndexes": [3, 1],
      "NBackTaskIndexes": [2, 1, 4, 3]
    }
  ]
```

### NbtProfiles

This section contains a list of profiles available in the NBackTask application.
For example:

```
"NbtProfiles": ["system", self"]
```

### Sets

This section contains a list of sets, each containing a pair of indexes of `SessionSetups` and `NbtProfiles`.
The sets are participant-wise, i.e. if `N` is the number of sets, then the participant with some ID will be assigned to complete tasks described in a set with the index equal to `(ID - 1) % N`.

Lets study an example. Let's say there are 4 sets (`N=2`) defined like this:

```
"Sets": [
    [
      {
        "SessionSetupIndex": 0,
        "NBackTaskProfileIndex": 0
      },
      {
        "SessionSetupIndex": 1,
        "NBackTaskProfileIndex": 1
      }
    ],
    [
      {
        "SessionSetupIndex": 0,
        "NBackTaskProfileIndex": 1
      },
      {
        "SessionSetupIndex": 1,
        "NBackTaskProfileIndex": 0
      }
    ],
    [
      {
        "SessionSetupIndex": 1,
        "NBackTaskProfileIndex": 0
      },
      {
        "SessionSetupIndex": 0,
        "NBackTaskProfileIndex": 1
      }
    ],
    [
      {
        "SessionSetupIndex": 1,
        "NBackTaskProfileIndex": 1
      },
      {
        "SessionSetupIndex": 0,
        "NBackTaskProfileIndex": 0
      }
    ]
  ]
```

Say, a participant with ID = 2 takes part in the study. Then the set index will be `(2 - 1) % 4 = 1`, i.e. the participant will be completing two sets of tasks, the first described as
```
  "SessionSetupIndex": 0, 
  "NBackTaskProfileIndex": 1
```
and teh second as
```
  "SessionSetupIndex": 1
  "NBackTaskProfileIndex": 0
```
That is, the first session will contain 8 non-randomized blocks, as the session with index 0 in the `SessionSetups` section is described as
```
  "Randomized": false,
  "Repetitions": 1,
  "CttLambdaIndexes": [1, 3],
  "NBackTaskIndexes": [1, 2, 3, 4]
```

After this session is complete, the applicaiton will quit automatically. The next time the applicaiton is launched and the same participant ID = 2 is provided, the session will contain 16 randomized block, as the session with index 1 is described as 
```
  "Randomized": true,
  "Repetitions": 2,
  "CttLambdaIndexes": [3, 1],
  "NBackTaskIndexes": [2, 1, 4, 3]`
```

Note that application determines which set to utilize by checking the participant's data log folder. In this example, it selected the set index equal to the number of existing folders inside of `P02` folder.

As there are 4 sets defined in this example, then participant with ID = 6 will be completing same tasks in the same order.

### Questions

This section contains a list of questions asked after each block of trials. Each question is describe as in the following example:

```
  "Type": 0,
  "Text": "How easy was the task?",
  "ID": "RATING",
  "ScaleMin": 1,
  "ScaleMax": 7,
  "ScaleMinText": "Very difficult",
  "ScaleMaxText": "Very easy"
```

So far (v0.6), the only type supported is `0` meaning the question is of the `scale` type with min and max values defined.
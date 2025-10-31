# Varjo Data Logger

Logs eye-gaze, head-gaze and hand location into a sinlge file.

## Hardware

Tested with Varjo XR-3 and XR-4 with UltraLeap Motion Controller.

## Dependencies

The required binary libraries are included into the repo.

Note that VarjoTrackerLib can be compiled from https://github.com/lexasss/VarjoTrackerLib.git. The compiled DLL file must be copied to this project `libs` folder.

## Command line

```
  -l, --log        Log file folder. IMPORTANT: it must not contain spaces! Default is 'C:/Users/<USERNAME>/Documents'.
  -o, --offset     Leap Motion ZYX offsets (comma-separated, no spaces). Default is '-6,15,0'.
  -h, --hide       Forces the console window to be hidden (minimized) while the tracking is on.
  -c, --cttip      IP address of the PC running CTT application. Default is '127.0.0.1'.
  -n, --nbtip      IP address of the PC running N-Back task application. Default is '127.0.0.1'.
  -m, --lmsip      IP address of the PC running Leap Motion Streamer application. Default is '127.0.0.1'.
  -s, --setup      JSON file with study configuration. Default is 'no value'.
  -v, --verbose    Debug info is printed in the verbose mode.
  -d, --debug      Sets to the debug mode.
```

## Notes

The room setup in Varjo Base must be reset once a user takes the upright poistion and faces toward the virtual desktop.
This allows proper headset rotation compensation for hand location data.

## Study

This tool can be used run a study that involves using [CTT](https://github.com/lexasss/ctt) and [NBackTask](https://github.com/lexasss/n-back-task) applications. If you plan such a study, you can specify IP addresses of the machines running these apps (`cttip` and `nbtip` parameters).

In addition, the log file with Varjo data can be extended with an additional Leap Motion hand tracker (the old LeapMotion model, not the UltraLeap model used to attach to the Varjo headset). If there is such a tracker, please download [LMStreaming tool](https://github.com/lexasss/LMStreaming) and provide the IP address of the machine running LMStreaming app in `lmsip` parameter.

Finally, you would need to specify the study configuration. You can run the Varjo Data Logger with `setup` set to some not yet existing JSON file, and it will create such a file for you with some default content. You then have to edit this file according to the descirption below.

### Study configuration file

Study configuration is specified in a JSON file that has 5 sections.

#### SessionSetups

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
    "NbtLayoutIndexes": [1, 2, 3, 4]
  },
  {
    "Randomized": true,
    "Repetitions": 2,
    "CttLambdaIndexes": [3, 1],
    "NbtLayoutIndexes": [2, 1, 4, 3]
  }
]
```

#### NbtProfiles

This section contains a list of profiles available in the NBackTask application.
For example:

```
"NbtProfiles": ["system", self"]
```

#### Sets

This section contains a list of sets, each containing a pair of indexes of `SessionSetups` and `NbtProfiles`.
The sets are participant-wise, i.e. if `N` is the number of sets, then the participant with some ID will be assigned to complete tasks described in a set with the index equal to `(ID - 1) % N`.

Lets study an example. Let's say there are 4 sets (`N = 4`) defined like this:

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
and the second as
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

#### Questions

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

So far (v0.6), the only supported type is `0`, meaning the question is of the `scale` type with min and max values defined.

#### Paths

This section describes the location of collected data. It consists of the data destination folder `Destination` where all participant data will be collected upon the app finishes its work. The other part is `FilesMasks` that contains is a list of `Path`-`Mask` pairs. For example:
```
"Paths": {
  "Destination": "..\\data",
  "FilesMasks": [
    {
      "Path": "D:\\data",
      "Mask": "*.txt"
    },
    {
      "Path": "D:\\Videos\\Varjo",
      "Mask": "*.mp4"
    },
    {
      "Path": "D:\\Videos\\Varjo",
      "Mask": "*.csv"
    }
  ]
}
```

After the session is over, the application traverses over this list, collects all the files that match the masks in the corresponding folders, and move them to the `{Destination}\PXX\{N} - {nbt-profile}` folder, where `PXX` is a participant folde (like `P03`), `N` is a 0-based session index, and `nbt-profile` is the NBackTask profile used in this session.

In this example, all log files will be moved to `data\P03` folder that will be created in the parent's folder (relative to this application). If the CTT and NBackTask were configure to store their log file in `D:\data`, then these log file will be moved to `..\data\P03\0 - system`. Also Varjo log data, XR view video (MP4) and gaze log file (CSV), will be moved to the same folder.
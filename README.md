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
  -m, --lmsip      [ >= v0.4 ] IP address of the PC running Leap Motion Streamer application. Default is '127.0.0.1'.
  -l, --log        Log file folder, must be without spaces. Default is 'C:/Users/<USERNAME>/Documents'.
  -o, --offset     Leap Motion ZYX offsets (comma-separated, no spaces). Default is '-6,15,0'.
  -h, --hide       Forces the console window to be hidden (minimized) while the tracking is on.
  -s, --setup      JSON setup file with a list of task configurations. Default is 'no value'.
  -t, --task       [ <= v0.5 ] Task ID to be loaded from th esetup file. Default is '-1' (task ID is computed from the participant ID, see below).
  -v, --verbose    Debug info is printed in the verbose mode.
  -d, --debug      Sets to the debug mode.
```

## Notes

The room setup in Varjo Base must be reset once a user takes the upright poistion and faces toward the virtual desktop.
This allows proper headset rotation compensation for hand location data.

## Tasks

Task configurations are stores in a JSON file The format of a task is the following:

```
    [{
      "Randomized": false|true,
      "Repetitions": N > 0,
      "CttLambdaIndexes": <array of lambda indexes as appear in CTT> application,
      "NBackTaskIndexes": <array of task indexes as appear in N-Back task application>,
    }]
```

## Experiment

In the current implementation, the setup file must contain at least 6 configurations.

When the setup file is defined, then the app runs two series of blocks (`CttLambdaIndexes` x `NBackTaskIndexes`).
Prior to start the first block, it asks for a participant ID (1-99). If ID was provided, it load the study configuration using ((ID - 1) % 16) as an index/key of the following array:

```
    { 0, new Condition(0, 4, TaskOrder.SystemFirst) },
    { 1, new Condition(1, 5, TaskOrder.SelfFirst) },
    { 2, new Condition(2, 4, TaskOrder.SystemFirst) },
    { 3, new Condition(3, 5, TaskOrder.SelfFirst) },
    { 4, new Condition(0, 5, TaskOrder.SystemFirst) },
    { 5, new Condition(1, 4, TaskOrder.SelfFirst) },
    { 6, new Condition(2, 5, TaskOrder.SystemFirst) },
    { 7, new Condition(3, 4, TaskOrder.SelfFirst) },
    { 8, new Condition(0, 4, TaskOrder.SelfFirst) },
    { 9, new Condition(1, 5, TaskOrder.SystemFirst) },
    { 10, new Condition(2, 4, TaskOrder.SelfFirst) },
    { 11, new Condition(3, 5, TaskOrder.SystemFirst) },
    { 12, new Condition(0, 5, TaskOrder.SelfFirst) },
    { 13, new Condition(1, 4, TaskOrder.SystemFirst) },
    { 14, new Condition(2, 5, TaskOrder.SelfFirst) },
    { 15, new Condition(3, 4, TaskOrder.SystemFirst) },
```

where `Condition ` is defined with `SystemTask`, `SelfTask`, and `TaskOrder`.
If the order is `SystemFirst`, then it uses `SystemTask` as an index for configuration in the first block, and `SelfTask` in the second block. For `SelfFirst` the  indexes are `SystemTask` and `SystemFirst`.
Before starting each block, the application asks N-Back task to load a configuration named `system` or `self`.

For each, session the application 
- sets lambda index in CTT
- sets task index in N-Back Task

For example, lets assume that configurations in the setup JSON file are defined as follow:

```
    [{
      "Randomized": false,
      "Repetitions": 1,
      "CttLambdaIndexes": [1, 3],
      "NBackTaskIndexes": [1, 2, 3, 4]
    },{
      "Randomized": false,
      "Repetitions": 1,
      "CttLambdaIndexes": [1, 3],
      "NBackTaskIndexes": [2, 1, 4, 3]
    },{
      "Randomized": false,
      "Repetitions": 1,
      "CttLambdaIndexes": [1, 3],
      "NBackTaskIndexes": [3, 4, 2, 1]
    },{
      "Randomized": false,
      "Repetitions": 1,
      "CttLambdaIndexes": [1, 3],
      "NBackTaskIndexes": [4, 3, 1, 2]
    },{
      "Randomized": false,
      "Repetitions": 1,
      "CttLambdaIndexes": [1, 3],
      "NBackTaskIndexes": [3, 4]
    },{
      "Randomized": false,
      "Repetitions": 1,
      "CttLambdaIndexes": [1, 3],
      "NBackTaskIndexes": [4, 3]
    }]
```

A pariticipant with ID=3 will follow the procedure described in the configuration with index ((3 - 1) % 16) = 2, i.e. `SystemTask = 2`, `SelfTask = 4`, and `TaskOrder = SystemFIrst`.
In first block, s/he will complete N-back tasks with indexes 3, 4, 2, and 1, first with lambda index set to 1, then same tasks with lambda index set to 3.
In the second block, s/he will complete N-back tasks with indexes 3 and 4, first with lambda index set to 1, then same tasks with lambda index set to 3.
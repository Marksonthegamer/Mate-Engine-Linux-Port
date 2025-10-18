# [Mate Engine](https://github.com/shinyflvre/Mate-Engine) - Custom Dance Player

## Technical Documentation

For more detailed instructions, refer to [MateEngine-CustomDancePlayer](https://github.com/maoxig/MateEngine-CustomDancePlayer).

### Dance File Instructions

Download all converted dance files and a sample pre-packaged dance file here:  
https://drive.google.com/drive/folders/1YU7-Hz-O8-9B2E58mxQxexJTBTCT42jr?usp=sharing

Creating a dance file (.unity3d) is easy: In Unity, package a `.anim` animation file, a corresponding `.controller` file, and an `.mp3`/`.ogg`/`.wav` audio file into an AssetBundle.

#### Dance File Structure (.unity3d AssetBundle)
Each dance uses a `.unity3d` AssetBundle containing the following resources (consistent naming recommended):

| Resource Type       | File Naming Convention          | Description                                                                                                                                                                                                          |
| ------------------- | ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Animator Controller | `[DanceName].controller`        | Simple single-state controller: Create an Animator Controller, drag the `[DanceName].anim` file into the controller window. Unity auto-generates a state, and the "Entry" transitions unconditionally to this state. |
| Audio File          | `[DanceName].wav`/`.mp3`/`.ogg` | Supports 3 common formats; duration must match animation length to avoid desynchronization.                                                                                                                          |
| Animation           | `[DanceName].anim`              | Contains dance animation data.                                                                                                                                                                                       |

#### Additional Notes

For optimal MMD facial and camera support, follow these standards when creating dance files:

**Facial expressions**: Use standard Japanese MMD BlendShapes (e.g., `あいうえお`), controlled via animation curves on the model's `Body` (`SkinnedMeshRenderer`).

**Camera animation**: Create `Camera_root/Camera_root_1/Camera` under the model. `Camera_root` controls overall position, `Camera_root_1` controls distance (z-axis), and the Camera component manages FOV. After creating the camera hierarchy as above, add an initial 180° rotation on the y-axis to `Camera_root`.


For MMD dance file creation, see: https://github.com/maoxig/VroidMMDTools
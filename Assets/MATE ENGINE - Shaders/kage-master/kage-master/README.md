# Kage
Casting shadows on an invisible plane.

UPDATE 2022/09/27:
 - Now support multiple lightings
 - Now support directional, point and spot lights


![](assets/aniamtion.gif)

[Watch this video on YouTube](https://youtu.be/Iku6OCW7sTQ)

![](https://github.com/Ending2015a/Assets/blob/master/ar.gif)

[Watch this viedo on YouTube](https://youtu.be/YGo8sTYMJ68)


Unity version: `2019.4.29f1` (It might work on any version of Unity.)

## Limitations
 - Only works for Built-in Rendering Pipeline
 - Does not work for `Skybox`. change your camera background to `Solid Color`.

<img src="https://user-images.githubusercontent.com/18180004/192334772-15f8a294-872a-43f0-a8f8-e9b29108a053.png" width="300">


## Tutorial

Set the `Cast Shadows` option to `Off` of your model that only draw shadows, then attach the `TransparentShadow` Material/Shader to it.

<img src="https://user-images.githubusercontent.com/18180004/192334102-59221b86-ce72-41db-a54d-62cb78cc54cd.png" width="300">
<img src="https://user-images.githubusercontent.com/18180004/192333409-b8436220-22ef-457f-ba5f-102e3c681671.png" width="300">

### Point lights and spot lights

Don't forget to enable the `Shadow Type` option of the lights.

<img src="https://user-images.githubusercontent.com/18180004/192334537-49a7ed01-8c5d-4b38-9ca4-3dc1508de958.png" width="300">

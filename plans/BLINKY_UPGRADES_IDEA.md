# overview

blinky is now working well as designed.

this idea will iterate on blinky to add more functionality.

# new features

- ensure blinky.lib contains all actual functionality and that blinky mod is just an interface to drive it
- refactor blinky.lib so that it is self-contained and DOES NOT reference any code from blinken.lib (blinken is the legacy mod that i plan to delete, blinky must re-implement all its own functionality)
- add new blinky.lib functionality "play animation" that allows submission of a data structure to drive an animation, instead of only being hard-coded to the built-in animation
- add new blinky.lib functionality "display static" which will just paint a static set of pixels (the input data should take a "reset" boolean which if true will turn off all pixels and then display whatever pixels are passed using a list of (x, y) pairs to define them.  if possible, do an intelligent pass which will just change the engine states of the relevant pixels instead of blindly resetting and then turning the new ones on.)
- expose both new features "play animation" and "display static" as RPC endpoints via the unladen-swallow.lib project.  the HTTP POST API payloads should take a vehicle ID to apply it to (and blinky should find the appropriate LCD grid its managing by vehicle ID), and an array of (x,y) pairs of pixels to paint.  the "display static" should get the additional "reset" option which will make it intelligently turn off pixels when applying the newly supplied pixels to light up.
- expose a "off" feature which just turns off all pixels
- use POST /blinky/animate for the play animation api endpoint (vehicle id + pixel data + speed) [reset is implicit as animation is fully controlled]
- use POST /blinky/static for the static endpoint (vehicle id + reset + pixel data)
- use POST /blinky/off for the off endpoint (just take vehicle id)


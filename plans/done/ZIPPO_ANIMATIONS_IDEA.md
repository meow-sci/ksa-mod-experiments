the zippo mod currently allows for a UI which can turn light parts on/off and change their color

we need to make changes to this mod

- all changes must be usable either from the ImGui UI or from a HTTP API RPC endpoint via unladen-swallow
- unladen-swallow HTTP API RPC endpoints should be added for zippo functionality
- the openapi spec should go into unladen-swallow.lib/openapi/zippo.yml
- new mod features
    - add a simple, single-step animation capability.  it should have the following features
        - start color selection
            - ImGui should have color picker widget AND filterable combobox with KSA.KSAColor.Xkcd colors
            - RPC should take either RGB or a named KSAColor.Xkcd color constant
        - end color selection (same way to define)
        - start intensity
        - end intensity
        - easing function + powers (similar to camera-controller-override mod)
        - duration (in fractional seconds)
        - both the color and intensity should be interpolated/lerped between their start/end values using the easing function for the rate to interpolate
        - uninterruptible
        - if another animation is submitted before one is running, queue them to run back to back.  maybe the animations always get pushed to a queue and the animation player checks that, but do something efficient

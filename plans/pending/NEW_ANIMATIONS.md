# overview

to add new animations that can be supported in the keyframe animation system.

each one should get a new collapisble header and follow similar patterns to the existing animations.

# animations

## shake (left/right)

"shake" the camera left/right (from the viewers perspective) N times with an an easing function that can be selected for between the back/forth (or use a similar oscilation function like the loopy orbit does), the goal is to make the shake back/forth motion be smooth.

it should appear as if someone was shaking their head back and forth.

the animation length, number of shakes and some kind of input into how harsh the shaking motion is if that's possible.

## zoom in

the opposite of the current zoom out animation, zoom in to the target.  support same parameters as the existing zoom out.

## zoom in to target offset

this is like zoom in except we want to zoom into some fixed offset of the center of the target.

the goal here is to enable zooming into the helmet of an astronaut game model by e.g. specifying 0.25m offset on Z axis for the target offset, meaning that this would be how far up their face is from their center on the Z axis.

support setting the offset on all three axis with a scale of 0.25m to 20m defaulting to 0.5m
i want to be able to make 3d shapes and animations.

however, i think doing a grid in 3d will be quite complex AND hard to control AND hard to create animations for.

the use cases i have in mind for animations don't need full volumetric 3d pixels, they could be easily achieved by having 1..N  rectabgular or cylinder grids (our current style

here's what I want to do:

- support multiple grids per vehicle
- each grid requires a name, add a text input that must be filled in when creating a grid
- no duplicate names allowed
- the imgui ui must now support multiple grids so put each under a collapsible header with its own set of the controls available to control the grid
- the stateful code must be refactored support managing each grid independently
- unladen-swallow existing RPC endpoints for blinky must be adapted so that they take both the vehicle id and grid name to operate on
- refactor the stateful code to make it easy to locate a grid by vehicle id + grid name so that rpc code can find it ad-hoc during rpc code execution

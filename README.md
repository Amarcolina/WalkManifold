# Walk Manifold
[![Demo Video](https://i.imgur.com/yt0x2q3.png)](https://www.youtube.com/watch?v=0C_UjfXC6t4)

[Try The WebGL Demo Yourself Here](https://amarcolina.github.io/WalkManifold/)

Walk Manifold is a library used for Agent movement that targets a specific kind of game/use case. It is based on the technique described by Casey Muratori in his video ["Killing The Walk Monster"](https://www.youtube.com/watch?v=YE8MVNMzpbo). It provides the following features which are not usually provided by movement systems:
 - **[Guaranteed Reversibility](https://github.com/Amarcolina/WalkManifold/edit/main/README.md#thoughts-on-reversibility)**: Impossible to create a situation where the player can move _into_ a space, but cannot then get back out. No longer need to worry about accidentally creating spaces that can trap the player.
 - **[Walkable Surface Visualization](https://github.com/Amarcolina/WalkManifold#visualization-tools)**: Visualize the _exact_ walkable area during edit-time and run-time, full-stop. You get to see exactly where the player can stand, _in addition_ to exactly where they can _get to_. This allows you to trivially find accidental 'holes' left while building a level that allows a player to get where they shouldn't.

In addition to these novel features, it also has many of the basic features you would expect from a movement system:
 - **Agent Height/Radius Config**: Move the player only where a capsule with a given height/radius can fit. Prevents moving too close to a wall, or under a too-low overhang.
 - **Agent Step-Height**: Allows the player to 'step up' onto features which are a certain height above the current floor level.
 - **Max Standing Angle**: Prevents standing on any surface with a slope greater than a specific angle.
 
The above features are emphasized at the expense of these other features, which are MISSING currently:
 - **No jumping**: The player cannot jump over / on-top of things right now.
 - **No walking off cliffs**: The player cannot walk off the edge of a cliff and fall down to a lower area.

This feature set is aimed at the common "Waking Simulator" style game, where you typically don't have movement mechanics other than getting from point A to point B. The guaranteed reversibility can be a critical feature for these kinds of games, where the act of getting stuck can be potentially game-breaking, especially when the game doesn't have mechanics like jumping to get the player out of many situations. The surface visualization is also **very** useful to have in these kinds of games, as many times the entire premise of the puzzle hinges on the players ability to reach certain areas of the map.

## This Repo
This repo contains a single Unity project that contains both the library package, as well as a separate demo. The library package is located at `Packages/com.walkmanifold`, check out the `How To Install` section for brief instructions on how to install it. The `Assets/` folder is the Assets folder of a regular Unity project that represents the demo used for this package as well. This demo is a good source of learning on how to use the package, and includes a very basic player movement script that interfaces with the ManifoldCharacterController.

To open the demo yourself, simply clone the repo and open the Project using Unity version 2021.3 LTS.

## How To Install
A few different ways you can install this into your own project:

1) Use the latest unitypackage from the release page to install it into your project.

2) The package is located in this repo at `Packages/com.walkmanifold`, you can simply copy this directly into your own project (also at Packages/com.walkmanifold) if you want to keep a local copy.

3) Add `"com.walkmanifold" = "https://github.com/Amarcolina/WalkManifold.git?path=/Packages/com.walkmanifold"` as a dependency in your `Packages/manifest.json` file.

## How To Use
There are only three steps that you need to do in order to start using this package for player movement.

1) Add a new ManifoldCharacterController component to your Player/Agent object. This replaces an existing Unity CharacterController if you are using one.

2) Create a new ManifoldSettings asset in your Project window (Create -> Manifold Settings), and assign it to your ManifoldCharacterController. The Settings asset contains all of the parameters needed to define how the agent can walk around on the manifold, and can be shared between multiple scripts / tools. The default settings should be sufficient for testing, and you can refine to be specific to your own needs.

3) Update your player script to use the new ManifoldCharacterController. As the script has the same Move/SimpleMove methods as the Unity CharacterController, it should be straightforward to update any player scripts you have. Alternatively, you can use the example script from this project (Located in Assets/Scripts) if you want a fresh starting place.

## Visualization Tools
![SceneViewPreview](https://user-images.githubusercontent.com/5723312/173162974-da530964-94ba-4448-89f1-6a3b902e0a16.gif)


In addition to supporting player movement, this method also provides an easy way to visualize the walkable surface during the edit process. You can invoke this at any point if you want to check to see if a new prop blocks the path, or to check to see if the player can reach a specific area through an unexpected hole.

To enable the debug mode, simply go to `Tools->Walk Manifold Debug`. This will open the Walk Manifold debug window. (Remember to assign the Settings property once you open the window!). While this window is open, it enables a few controls in the Scene View:
 - Press G to visualize the walkable surface near the mouse position.
 - Scroll the mouse while pressing G to change the size of the visualization area.
 - Middle click while pressing G to stamp the visualization area as an actual GameObject that you can persist and manipulate further.

Stamped areas will use the ManifoldDebugView component to show the nearby grid. This component allows both edit-time visualization, as well as run-time supported in builds, so you can visualize the surface on your target platform.

## Isolate Reachable
It is worth noting the specific `Isolate Reachable` option that is present in both the Manifold Debug Window, as well as in the Manifold Debug View. This will specifically highlight _only_ the part of the grid that could be reached from the center point of the visualization area (the mouse position when using the Window, the transform position when using the Component).

While calculating the reachable area, only the current visualization space will be considered. This means certain areas might be marked as unreachable if the path needed to reach them goes outside the visualization region. In general calculating true reachability would need to consider the entire world, but a local reachability check is still very useful for checking to see if there are unexpected holes in a wall or constructions. The debug view can also get quite large, and will do the surface calculations async, which can allow you to visualize larger areas easily.

## Performance
There is plenty of optimization that could be done, but the code is in a reasonably performant state. It has zero GC cost after creation, and uses Burst jobs where it can. The performance of the tool is directly proportional to the number of cells of the grid that need to be generated. For the built-in ManifoldCharacterController this is usually a very small portion of the grid, as it only requires the cells covered by the requested movement vector. If your cell size is roughly comparable to the maximum move delta, this results in the number of cells generated ranging from a 1x1 to a 2x2.

The code contains profiler markers, which should aid any profiling you need to do for the system.

TODO: A more detailed account of the specific costs of the system. Currently the cost is dominated by calls into the Unity physics system. It uses raycasts and capsule checks to generate and refine the cells. Currently the system uses the physics queries directly for maximum compatibility, but it might be better in the future to try to take a local 'snapshot' of the physics scene around the query so that we can avoid the slower physics calls (that are also non-thread safe).

Anecdotal testing resulted in a cost of 0.1-0.3 ms for player movement on my own PC.

## Manifold Settings
Configuring the Manifold Settings will decide how the Manifold is generated for a specific Agent. Note that if you have two different agents with different navigational needs, you will need one settings asset for each of them, and they will each generate their own Manifold for navigating.

 - **Agent Height**: The grid will only provide a surface where there is enough floor-to-ceiling headspace to accomodate an agent of this height.
 
 - **Agent Radius**: The grid will only provide a surface where there is enough left-to-right space to accommodate an agent of this size.
 
 - **Step Height**: The amount of vertical distance an agent can step up from one part of the manifold to another. Any step that is higher than this will not be traversable.
 
 - **Max Surface Angle**: The grid will only provide a surface where the angle is less than this value.
 
 - **Cell Size**: The size in meters of each cell of the grid. In general features smaller than this size will not be represented accurately. Larger values can lose important features, but also help to smooth out movement along edges.
 
 - **Edge Reconstruction**: Allows reconstruction of the edge of the grid to provide a more accurate surface. If disabled only fully occupied cells will be present in the grid. Not usually recommended to be turned off.
 
 - **CornerReconstruction**: Allows reconstruction of the corners of the grid to provide a more accurate surface. If disabled corners will have a truncated appearance. Corner reconstruction is not currently perfect, and best reconstructs corners that are aligned to the grid.
 
 - **Reconstruction Iterations**: The number of additional physics queries to be used during edge reconstruction. More iterations results in greater quality but increased cost. Larger values have diminishing returns.
 
## Thoughts On Reversibility
Reversibility is a pretty strong thing to guarantee, so I thought I’d share some thoughts on the matter as well. As far as I understand, as long as the scene remains static, reversibility is both a solid concept that is well defined, and also guaranteed by this algorithm. In a static scene, the walkable surface mesh will also remain static, which allows us to analyze what properties a static surface mesh has. In this case, if each operation moves us from one point on the mesh to another connected point on the mesh, we can inductively understand that we must always stay on the same connected region of this static mesh.

I put so much emphasis on the ‘static’ part, since it seems like any issues with reversibility (which this method at least) all come from the non-static pieces of a level. As soon as the walkable surface is changing frame-to-frame, it becomes a little less defined what ‘reversibility’ means. At the start of a frame, the player needs to determine where on the walkable surface they are, so that they can move to another part of the walkable surface. This first operation, deciding where on the walkable surface they are, is not well defined in the presence of a changing surface.  If the player is standing on a platform that vanishes, that is a pretty clear case where no system could come up with a reasonable solution. When you go to try to figure out where on the surface you are, so that you can do your move, you won’t find any surface near where you are standing! This leads to what I’ve been thinking about as the ‘panic or resolve’ conundrum:

Panic: Assume something has gone terribly wrong.  Trigger some sort of special code that teleports the player to some gameplay-specific known-good position, like the start of the level, or the main hub.

Resolve: Try to use some heuristic to find where the player _should_ be.  For example, maybe you can do a neighborhood search to find the nearest walkable cell.  Or maybe you rewind in time until you can find a suitable surface.

I call this a conundrum because both solutions can provide a negative gameplay experience. If we Panic too aggressively, players might constantly be confused as to why they are teleporting to random locations.  If we try to Resolve instead though, our heuristic might put us in an area that is not actually correct. Who is to say that the nearest cell is a valid gameplay location to stand? Maybe the most recent valid position is no longer valid due to another action that happened since the player stood there?

I don’t have a solid solution to this problem, other than to develop more robust Resolution heuristics, and to do my best to design levels that are robust enough. Vanishing platforms, walls that squish you, these are examples of operations that can change the walkable surface so drastically that there is no good way to tell where the player should go.

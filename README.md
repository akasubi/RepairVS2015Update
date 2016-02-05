RepairVS2015Update
==================

What it does
------------

After applying updates, Visual Studio 2015 sometimes (or, in the case of my 
machine, always) shows *"The 'package name' package did not load correctly"* 
error messages.

RepairVS2015Update (usually) attempts to solve the problem, by deleting the 
CompontenModelCache directory and fixing the Collections.Immutable binding 
redirect version, as suggested in the answers to 
<a href="http://stackoverflow.com/questions/31547947/packages-not-loading-after-installing-visual-studio-2015-rtm">
this Stackoverflow question.</a>

!(Screenshot.png)

Why I wrote it
--------------

The VS2015 updates have an annoying habit of resetting the Collections.Immutable 
binding redirect version to the incorrect value "1.1.36.0" every single time 
something is updated. I had enough of editing  "devenv.exe.config" and 
navigating to and deleting the CompontenModelCache directory again and again, 
so I wrote this utility to automate it.

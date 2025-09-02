# Mac keys configuration

![Mac](./mac.jpg)

Macbook is terrific piece of hardware with great shiny bright retina screen with resolution of whooping 2880x1800 (3k), soft large keyboard (before notorious butterfly) and sturdy design. He is common guest in every second Hollywood movie, actually if you ever see a laptop in a movie chances are it will probably be Mac. He is a true star. Still there are few things that will annoy every user that was not born with silver spoon (ups. I mean Mac) and has gotten used to windows before or has to connect to windows every now and then using his precious Mac:

1. Applications are not closing when you close all windows
2. Alt-Tab will not bring up minimized applications
3. Alt and Windows keys are switched
4. There is no Insert key
5. There is no PrintScreen key

Notable mentions - Home, End,  Page Up, Page Down and Delete are also not there but can be emulated with Fn + cursor keys and Fn + backspace.

For 1. and 2. we just learn to live with it. With 3. 4. and 5. we will deal in the rest of this document. 

If you install Windows on Mac laptop 1. and 2. wil be solved. 3. 4. and 5. will still be a problem and we will describe how to fix it on both internal and external keyboard.

Now you must be thinking - Yeah right, you will solve Insert and PrintScreen by using external keyboard. No we will achieve Insert and PrintScreen keys even on internal keyboard without external keyboard connected.

Wanna see how? Follow me reader!


## Mac OS configuration

Here we describe configuration of Mac OS. We are not in Citrix RDP or in Microsoft Demote Desktop or in Windows. Plain Mac. So you are proud new owner of MacBook, you love your new toy, everything is fine, everything works, life has a meaning. It is little funny that Copy, Paste, Find, ... are actually Cmd-C and Cmd-V Cmd-F instead of Ctrl-C Ctrl-V Ctrl-F, but you get used to it. After all, Cmd key is closer, so it is easier to type combos - no little finger gymnastics. You are thinking about switching Control and Command but then you would have to switch it back in Terminal where Ctrl-C and Ctrl-V still have some  meaning (interrupt, insert) or you would have to fix Alt-Tab which would now be Ctrl-Tab so you just keep everything as is.
Problems really start when you connect external keyboard. First thing you notice is that Mac key is not really Alt - it is more like Windows key on a wrong position. Oh nooooo, why Apple, why?


## Citrix configuration

Citrix is very tricky to configure everything to work fine in Windows RDP session. Citrix likes to block some keys and key combos. I was searching for years for good setup that would enable Alt-Tab and Alt-Shift-Down for multi-cursor in Notepad++ or VSCode and I think i found it. Still up to today I didn't find a way to get Ctrl-Alt-Down to get through so I reconfigured VSCode to use Alt-Shift-Down instead - same as Notepad++.


## Microsoft Remote Desktop configuration

Configuring Citrix RDP will not fix keys in Microsoft Remote Desktop. We will fix them.


## Microsoft Windows configuration

If you by any change manage to install Windows on Macbook here you can see how to configure keys to make your life easier or just worth living.

Citrix keyboard mappings for Mac

On Mac internal keyboard is not changed:
Ctrl sends Ctrl
Left Option sends Alt
Left Command sends Windows

External keyboard is changed in Karabiner:
Ctrl sends Ctrl
Win send Option because this is Option position on Mac
Alt sends Command because this is normal position for Command key on Mac

So after maping on both internal and external keyboard order or keys is:
Ctrl
Option Alt
Command Win

Now maping in citrix is:
Send Control character using: Control
Send Alt characer using: Command because Command in on normal position for Alt on windows keyboard
Send Windows logo key using Command (right) checkbox is selected

Now in karabiner we configure two more rules:
In Citrix send Tab with Fn so that Citrix doesn't block it
In Citrix send right Command on left Option (Win key) - this will work since Citix expects right ctrl as Win key (check above)

Additionally in RDP left alt and left win are switched on all keyboards.


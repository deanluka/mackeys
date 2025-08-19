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
Send Control character using: ^ Control
Send Alt characer using: X Command (left) because Command in on normal position for Alt on windows keyboard
Send Windows logo key using X Command (right) checkbox is selected

Now in karabiner we configure two more rules:
In Citrix send Tab with Fn so that Citrix doesn't block it
In Citrix send right Command on left Option (Win key) - this will work since Citix expects right ctrl as Win key (check above)

Additionally in RDP left alt and left win are switched on all keyboards.

Karabiner files are in /Users/dean/.config/karabiner/assets/complex_modifications:

citrix.json - Ctrl, Windows, Alt order (normal windows order - we use this one)
citrix2.json - Ctrl, Alt, Windows order (Mac order - not used)
ctrl-alt-down.json - tried to make ctrl-alt-down work in citrix by adding fn modifier but it doesn't work
swap_alt_cmd_rdp.json - Swap left alt and left win for RDP

"description": "In Citrix, add fn modifier to tab so it is forwarded",
"manipulators": [
    {
        "from": {
            "key_code": "tab",
            "modifiers": { "optional": [ "any" ] }
        },
        "to": {
            "key_code": "tab",
            "modifiers": [ "fn" ]
        },
        "type": "basic",
        "conditions": [
            {
                "type": "frontmost_application_if",
                "bundle_identifiers": [
                    "^com\\.citrix\\.XenAppViewer$",
                    "^com\\.citrix\\.receiver\\.icaviewer\\.mac$"
                ]
            }
        ]
    }
]

"description": "In Citrix, change left_option to Windows logo key via \"Send Windows logo key using ⌘ Command (right)\"",
"manipulators": [
    {
        "from": {
            "key_code": "left_option",
            "modifiers": { "optional": [ "any" ] }
        },
        "to": {
            "key_code": "right_command"
        },
        "type": "basic",
        "conditions": [
            {
                "type": "frontmost_application_if",
                "bundle_identifiers": [
                    "^com\\.citrix\\.XenAppViewer$",
                    "^com\\.citrix\\.receiver\\.icaviewer\\.mac$"
                ]
            }
        ]
    }
]
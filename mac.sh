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
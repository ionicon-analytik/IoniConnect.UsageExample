IoniConnect.UsageExample
===========

This is a working example of performing common tasks with the
`Ionicon AME2.0` API, such as starting/stopping a *Measurement*,
creating/editing *Actions*, triggering them, collecting data in
realtime, et.c.

A running instance of the `IoniConnect.API` is required. This
may be an actual `PTR`-instrument or a local *DEMO*-server instance.


Prepare a DEMO.REPLAY in AME
-------

This assumes the `AME_launcher` has been installed with
- [x] add peakdame to PATH
- [x] create Desktop shortcut (DEBUG)
checked. The installer *can* be run again to add these options.

(Otherwise, `pd.exe` is found in `C:\Program Files\AME_launcher\_exec\python\Scripts`)


Open a terminal or PowerShell and enter the following command:
$ pd record extract -f <path-to-replay-file>

> Note:
>  A `D:` drive must be attached!

This installs a `.REPLAY`-file and extracts into
- `C:\Ionicon\AME\Recipes`
- `D:\AMEData\<date-of-the-record>`

Start the `AME_launcher` from the Desktop (DEBUG) shortcut to
launch into *headless* (i.e. w/o instrument attached) mode.
Each replay installed can now be selected from the main drop-down
menu and started with either a button click or an API call just
like a regular measurement.

### Limitations

Since this is a replay of a previously recorded measurement, 
it naturally lacks some interactivity. Triggering an `Action`
will not work, however the `Action-Editor` will be functional.

Normally, each new measurement (and `New-Folder-Action`) will
create a directory in `D:\AMEData` with the *current* date-time
and then populate it with the measured data. Since this is a
replay, the date-time is *frozen* at the time of recording, even
though an (empty) measurement directory will be created.


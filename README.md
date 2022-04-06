# Unity Sweeper

This plugin helps you sweep up your project - it detects unused files, removes it from project and create backup exported as unity package.

Using it is really simple. You can initiate deleting unused files trough `Window` menu.

![Window menu choices](Images/01.png)

Options are:
* _Only resource_ - Will sweep everything except scripts
* _Unused by Editor_ - Will sweep everything used by game or editor, including scripts
* _Unused by Game_ - Will sweep everything used by game, including scripts
* _Clear cache_ - Deletes cache file that is located under `Assets/referencemap.xml`

If you choose any of first three options, it will run a search after which it will show screen like this:

![Sweep window](Images/02.png)

...from where you can review assets and check which you want to exclude. It will also remove empty directories. Clicking _Exclude from Project_ button will create Unity Package with current date and time at location `PROJECT_ROOT/BackupUnusedAssets`.

Enjoy!

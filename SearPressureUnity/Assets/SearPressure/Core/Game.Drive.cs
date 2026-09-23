using System;
using System.Collections.Generic;

namespace SearPressure
{
    // Placeholder: the Delivery Runs driving game is ported in this file.
    public sealed class DriveState { public string phase = "over"; public bool hand; public int k; }

    public sealed partial class Game
    {
        public DriveState D;
        void renderDrive(double dt) { }
        void updateDrive(double dt) { }
        void engineSet(double v) { }
        void engineStop() { }
        void drivePointerDown(int id, double x, double y) { }
        void drivePointerMove(int id, double x, double y) { }
        void drivePointerUp(int id) { }
        void driveEnterKey() { }
        public int driveStars(int k, double coins) => 0;
        public bool driveOpen(int k) => false;
        public List<int> newDriveRuns() => new List<int>();
        public void startDrive(int k, bool tutorial = false) { }
        public void openDriveIntro(int k) { }
        double driveTimeLeft() => 99;
    }
}

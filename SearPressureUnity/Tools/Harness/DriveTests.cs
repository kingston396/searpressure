using System;
using System.Linq;
using SearPressure;

// Delivery Run scenarios.
static partial class Tests
{
    static partial void RunDrive(string which, ref bool handled)
    {
        switch (which)
        {
            case "drive": Drive(); break;
            case "drivetut": DriveTut(); break;
            case "driveend": DriveEnd(); break;
            case "driveall": Drive(); DriveTut(); DriveEnd(); break;
            default: return;
        }
        handled = true;
    }

    static void Status(Game g, string tag)
    {
        var D = g.D;
        Console.WriteLine($"[{tag}] phase={D.phase} coins={D.coins} delivered={D.delivered} bags={D.bags} time={D.time:0.00} car=({D.car.x:0.00},{D.car.y:0.00}) speed={Math.Sqrt(D.car.vx * D.car.vx + D.car.vy * D.car.vy):0.00} engine={g.engineLevel:0.00} tut={(D.tut != null ? D.tut.i.ToString() : "-")} traffic={D.traffic.Count} peds={D.peds.Count} pops=[{string.Join(" | ", D.pops.Select(p => p.text))}]");
    }

    // A save that has already had the driving lesson.
    static HarnessPlatform DrivePlatform(bool lessonDone)
    {
        var p = new HarnessPlatform { unlock = true };
        if (lessonDone) { var s = new SaveData { driveTut = true }; p.saveJson = s.ToJson(); }
        return p;
    }

    static void Drive()
    {
        var p = DrivePlatform(true);
        var g = Program.NewGame(p);
        Program.Run(g, 0.2);
        g.startDrive(0);
        Program.Run(g, 1.0);
        Program.Shot(g, "drive_count.png");
        Status(g, "count");
        // Seeded city and traffic: these should match the web version run with ?calm.
        var D = g.D;
        Console.WriteLine($"city: truck=({D.city.truck.x},{D.city.truck.y}) props={D.city.props.Count} doors={D.city.doors.Count} ramps={string.Join(" ", D.city.ramps.Take(3).Select(r => $"{r.x},{r.y},{r.horiz}"))} "
            + $"traffic0={string.Join(" ", D.traffic.Take(3).Select(t => $"{t.x:0.00},{t.y:0.00},{t.dir},{t.color}"))} ped0={D.peds[0].x},{D.peds[0].y},{D.peds[0].shirt} car.a={D.car.a:0.00}");
        Program.Run(g, 2.4);
        Status(g, "go");
        g.input.keys["KeyW"] = true;
        Program.Run(g, 1.5);
        Program.Shot(g, "drive_early.png");
        Status(g, "early");
        Program.Run(g, 1.0);
        g.input.keys["KeyD"] = true; g.KeyDown("Space", false);
        Program.Run(g, 0.8);
        g.input.keys["KeyD"] = false; g.KeyUp("Space");
        Program.Run(g, 2.5);
        Program.Shot(g, "drive_later.png");
        Status(g, "later");
        // Pull up at the customer: a delivery.
        g.input.keys["KeyW"] = false;
        D.car.x = D.target.x; D.car.y = D.target.y; D.car.vx = D.car.vy = 0;
        Program.Run(g, 0.1);
        Status(g, "delivered");
        Program.Shot(g, "drive_delivered.png");
        Console.WriteLine("sounds: " + string.Join(",", p.sounds.Distinct()));
    }

    static void DriveTut()
    {
        var p = DrivePlatform(false);
        var g = Program.NewGame(p);
        Program.Run(g, 0.2);
        g.startDrive(0);
        Program.Run(g, 3.6);
        Program.Shot(g, "drivetut_0.png");
        Status(g, "tut start");
        g.input.keys["KeyW"] = true;
        Program.Run(g, 1.5);
        Status(g, "tut after gas");
        Program.Shot(g, "drivetut_1.png");
        // Skip to the end of the lesson with Enter on the cards that have a button.
        g.D.tut = new DTut { i = 5 };
        Program.Run(g, 0.1);
        Program.Shot(g, "drivetut_5.png");
        g.KeyDown("Enter", false); Program.Run(g, 0.1);
        Status(g, "tut 6");
        g.KeyDown("Enter", false); Program.Run(g, 0.5);
        Status(g, "tut done");
        Program.Shot(g, "drivetut_go.png");
    }

    static void DriveEnd()
    {
        var p = DrivePlatform(true);
        var g = Program.NewGame(p);
        Program.Run(g, 0.2);
        g.startDrive(1);
        Program.Run(g, 3.5);
        g.input.keys["KeyW"] = true;
        Program.Run(g, 2);
        g.D.coins = 75; g.D.delivered = 3; g.D.tips = 8; g.D.hits = 1; g.D.hitLoss = 10;
        g.D.time = 0.05;
        Program.Run(g, 0.5);
        Program.Shot(g, "driveend_over.png");
        Status(g, "over");
        Program.Run(g, 2);
        Program.Shot(g, "driveend_results.png");
        Console.WriteLine($"results: {g.results.no} / {g.results.name} stars={g.results.stars} served={g.results.served} failed={g.results.failed} score={g.results.score} next=\"{g.results.next}\" wallet=\"{g.results.wallet}\" engine={g.engineLevel}");
        // The intro card for a run.
        g.toMenu();
        g.openDriveIntro(0);
        Program.Run(g, 0.3);
        Program.Shot(g, "drive_intro.png");
    }
}

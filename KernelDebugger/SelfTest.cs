using Kernel;

namespace KernelDebugger;

/// <summary>
/// Checks the domain layer can write its data down and read it back with
/// nothing lost.
///
/// This exists because the jobs file is somebody's round: names, prices, what
/// is owed and years of work done. There is no way to try a change to how it
/// is stored on a phone without risking that, and a change that looks right
/// in the code can still drop a field on the way through the serializer -
/// silently, because an element the reader does not know about is ignored
/// rather than complained about.
///
/// So it is run for real: build a round, save it, throw it away, load it
/// back, and say what is missing. Run it with "dotnet run -- selftest".
/// </summary>
public static class SelfTest
{
    private const string Folder = "kernel-selftest";

    private static int _failures;

    public static int Run()
    {
        Console.WriteLine("Kernel self test");
        Console.WriteLine("================");

        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Folder);

        //a folder of its own, emptied first, so a run never reads what the
        //last one left behind
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        Directory.CreateDirectory(folder);

        try
        {
            RoundAndDurationSurviveASave();
            ARoundBelongsToEveryVisit();
        }
        catch (Exception ex)
        {
            Fail($"threw: {ex}");
        }

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "PASSED" : $"FAILED - {_failures} problem(s)");

        return _failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// the round and the time a job takes are still there after the file has
    /// been written and read back
    /// </summary>
    private static void RoundAndDurationSurviveASave()
    {
        Console.WriteLine();
        Console.WriteLine("A round and a duration survive being saved and loaded");

        Reset();

        Job first = AddJob("12", "High Street", 10.50f);
        first.SetRound("Tuesday");
        first.EstimatedTime = 35;

        Job second = AddJob("14", "High Street", 12f);
        second.SetRound("Tuesday");
        second.EstimatedTime = 40;

        Job other = AddJob("1", "The Green", 8f);
        //deliberately left on no round

        Job.Save(Folder);

        Reset();
        Job.Load(Folder);

        Check("the jobs are all there", Job.Query().Count == 3, $"{Job.Query().Count} came back");

        Job read = Find("12", "High Street");
        Check("the round survived", read != null && read.Round == "Tuesday", read == null ? "job missing" : $"round was '{read.Round}'");
        Check("the duration survived", read != null && read.EstimatedTime == 35, read == null ? "job missing" : $"duration was {read.EstimatedTime}");

        Job readOther = Find("1", "The Green");
        Check("work on no round stays on no round", readOther != null && !readOther.HaveRound,
            readOther == null ? "job missing" : $"round was '{readOther.Round}'");
    }

    /// <summary>
    /// a round put on a job is on every visit of it, including the ones
    /// already done and the ones still to come
    /// </summary>
    private static void ARoundBelongsToEveryVisit()
    {
        Console.WriteLine();
        Console.WriteLine("A round is on every visit of the job");

        Reset();

        Job job = AddJob("20", "Mill Lane", 15f);
        job.SetFrequence(4, FrequenceType.Week);

        //done once, which is what makes the next visit
        job.MarkJobDone(true);

        List<Job> visits = job.EveryVisit();
        Check("marking it done made another visit", visits.Count == 2, $"{visits.Count} visit(s)");

        //the round goes on after the first visit has been done, which is the
        //case that used to leave the next clean on no round
        job.SetRound("Mill Lane Round");

        foreach (Job visit in job.EveryVisit())
            Check($"visit {visit.Id} is on the round", visit.Round == "Mill Lane Round", $"round was '{visit.Round}'");

        Job.Save(Folder);
        Reset();
        Job.Load(Folder);

        foreach (Job visit in Job.Query())
            Check($"visit {visit.Id} is still on the round after a save", visit.Round == "Mill Lane Round",
                $"round was '{visit.Round}'");
    }

    private static Job AddJob(string number, string street, float price)
    {
        Job j = new Job();
        j.Address = new Location()
        {
            PropertyNameNumber = number,
            Street = street,
            City = "Town",
        };
        j.Price = price;
        j.Name = Job.DefaultJobName;
        j.DueDate = DateTime.Now.Date;

        Job.Add(j);
        return j;
    }

    private static Job Find(string number, string street)
    {
        return Job.Query().Find(x => x.Address != null
            && x.Address.PropertyNameNumber == number
            && x.Address.Street == street);
    }

    private static void Reset()
    {
        Job.DeleteData();
        Job.Reset();
    }

    private static void Check(string what, bool passed, string detail)
    {
        Console.WriteLine($"  {(passed ? "ok  " : "FAIL")}  {what}{(passed ? string.Empty : $"  ({detail})")}");

        if (!passed)
            _failures++;
    }

    private static void Fail(string what)
    {
        Console.WriteLine($"  FAIL  {what}");
        _failures++;
    }
}

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
            ARoundWithNothingLeftOnItIsNotARound();
            AHouseIsCountedOnceHoweverManyVisitsAreOut();
            ACleanThatWasDoneCountsAfterTheJobIsCancelled();
            ASharedWorkListSurvivesTheTripThereAndBack(folder);
            CancellingBookedInWorkTakesItOffTheDay();
            AWriteOffLeavesARecord();
            APriceRiseTakesEffectOnTheDayItSays();
            ASkipIsMeasuredFromTheDayItWasSkipped();
            EachBankAccountKeepsItsOwnLayoutAndItsOwnReferences(folder);
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
    /// A skipped job comes back round from the day you were there and passed
    /// it over, not from the day it was due.
    ///
    /// Measured off the due date, a house that was already overdue was pushed
    /// out from a date in the past - so a weekly job a fortnight late, skipped
    /// today, came back due a week ago: still on the list, still red, and the
    /// skip looked like it had done nothing at all.
    /// </summary>
    private static void ASkipIsMeasuredFromTheDayItWasSkipped()
    {
        Console.WriteLine();
        Console.WriteLine("A skip is measured from the day it was skipped");

        Reset();

        DateTime today = DateTime.Now.Date;

        //a fortnight overdue, which is the case that went wrong
        Job late = AddJob("5", "Mill Lane", 10f);
        late.SetFrequence(1, FrequenceType.Week);
        late.DueDate = today.AddDays(-14);

        late.SkipJob(today);

        Check("it comes back a week after the day it was skipped",
            late.DueDate.Date == today.AddDays(7), late.DueDate.ToShortDateString());
        Check("and it is not still overdue", late.DueDate.Date > today, late.DueDate.ToShortDateString());
        Check("the skip is on the day it happened", late.DateSkipped.Date == today,
            late.DateSkipped.ToShortDateString());

        //clearing the skip puts back the date it had, which can no longer be
        //worked out by subtracting the days it was pushed out by
        late.UnSkipJob();
        Check("clearing the skip puts the date it had back",
            late.DueDate.Date == today.AddDays(-14), late.DueDate.ToShortDateString());
        Check("and it is not skipped any more", !late.HaveSkipped, "still skipped");

        //a house not due for months, skipped by mistake, must not be pulled
        //forward to next week by it
        Job later = AddJob("7", "Mill Lane", 10f);
        later.SetFrequence(1, FrequenceType.Week);
        later.DueDate = today.AddDays(60);

        later.SkipJob(today);

        Check("skipping work that is not due yet does not pull it forward",
            later.DueDate.Date == today.AddDays(60), later.DueDate.ToShortDateString());

        //a four weekly job goes out four weeks from the day it was skipped
        Job monthly = AddJob("9", "Mill Lane", 10f);
        monthly.SetFrequence(4, FrequenceType.Week);
        monthly.DueDate = today.AddDays(-3);

        monthly.SkipJob(today);

        Check("a four weekly job goes out four weeks from the skip",
            monthly.DueDate.Date == today.AddDays(28), monthly.DueDate.ToShortDateString());

        Job.Save(Folder);
        Reset();
        Job.Load(Folder);

        Job readBack = Find("9", "Mill Lane");
        Check("the skip survived a save", readBack != null && readBack.HaveSkipped,
            readBack == null ? "job missing" : "not skipped");
        if (readBack == null)
            return;

        readBack.UnSkipJob();
        Check("and clearing it after a save still puts the date back",
            readBack.DueDate.Date == today.AddDays(-3), readBack.DueDate.ToShortDateString());
    }

    /// <summary>
    /// A price rise reaches the visits it should and no others.
    ///
    /// The whole point of a rise carrying a date is that it is agreed before
    /// it happens, so the visit due next week stays at the old price while a
    /// visit that does not exist yet comes out at the new one. A clean
    /// already written up must never be repriced: it, its payment and the
    /// customer's balance are one record.
    /// </summary>
    private static void APriceRiseTakesEffectOnTheDayItSays()
    {
        Console.WriteLine();
        Console.WriteLine("A price rise takes effect on the day it says");

        Reset();

        DateTime today = DateTime.Now.Date;

        Job job = AddJob("3", "Mill Lane", 10f);
        job.SetFrequence(1, FrequenceType.Week);
        job.DueDate = today;

        //a clean already written up, at the old price
        job.MarkJobDone(today, true);

        Job outstanding = job.EveryVisit().Find(x => !x.IsCompleted);
        Check("marking it done made another visit", outstanding != null, "none found");
        if (outstanding == null)
            return;

        //agreed a fortnight out, so neither the clean just done nor the one
        //due next week is at the new price
        int repriced = job.SetPriceRise(12f, today.AddDays(14));

        Check("nothing on the round changed price yet", repriced == 0, $"{repriced} changed");
        Check("the clean already done keeps what it was charged", job.Price == 10f, $"{job.Price}");
        Check("and so does the visit due before the day", outstanding.Price == 10f, $"{outstanding.Price}");
        Check("the rise is on the books", outstanding.HavePriceRise && outstanding.PriceRiseStillToCome,
            outstanding.PriceRiseText);

        Job.Save(Folder);
        Reset();
        Job.Load(Folder);

        Job read = Job.Query().Find(x => !x.IsCompleted);
        Check("the rise survived a save", read != null && read.PriceRiseTo == 12f,
            read == null ? "job missing" : $"{read.PriceRiseTo}");
        if (read == null)
            return;

        Check("with the day it starts", read.PriceRiseDate.Date == today.AddDays(14),
            read.PriceRiseDate.ToShortDateString());
        Check("and what it is going up from", read.PriceRiseWas == 10f, $"{read.PriceRiseWas}");
        Check("the clean already done is still at the old price", read.CurrentPrice == 10f, $"{read.CurrentPrice}");

        //the next visit is generated the far side of the day, so it comes out
        //at the new price with nobody having had to remember
        read.MarkJobDone(today.AddDays(7), true);

        Job next = Job.Query().Find(x => !x.IsCompleted);
        Check("the visit after the day is at the new price", next != null && next.Price == 12f,
            next == null ? "none found" : $"{next.Price}");
        Check("the one before it was left as it was charged", read.Price == 10f, $"{read.Price}");
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

    /// <summary>
    /// The stats page showed a "No Round" with no houses, no time and no
    /// value in it while every house was on a round.
    ///
    /// The rounds were grouped off each visit rather than off the job, so a
    /// house whose finished visits had been left on no round - which is what
    /// an old file looks like, and what the load time repair cannot reach
    /// without a base id - went into two groups at once. The one made of the
    /// finished visits had nothing outstanding in it, so it drew a row of
    /// noughts.
    /// </summary>
    private static void ARoundWithNothingLeftOnItIsNotARound()
    {
        Console.WriteLine();
        Console.WriteLine("A round with nothing left on it is not a round");

        Reset();

        Job job = AddJob("30", "Church Row", 14f);
        job.SetFrequence(4, FrequenceType.Week);

        //done once, which is what makes the next visit
        job.MarkJobDone(true);
        job.SetRound("Thursday");

        //an old file: the visit already done was left on no round while the
        //one still to come is on Thursday
        Job finished = Job.Query().Find(x => x.IsCompleted);
        Check("there is a finished visit to leave behind", finished != null, "none found");

        if (finished != null)
            finished.Round = string.Empty;

        List<RoundStats> rounds = RoundStats.ByRound(12);

        Check("no round made out of finished visits", !rounds.Exists(x => x.HousesOnTheRound == 0),
            $"{rounds.Count} round(s): {Describe(rounds)}");
        Check("the house is on Thursday", rounds.Exists(x => x.Round == "Thursday" && x.HousesOnTheRound == 1),
            Describe(rounds));

        //and work that really is on no round still says so
        AddJob("1", "The Green", 8f);

        rounds = RoundStats.ByRound(12);

        Check("work on no round is still counted",
            rounds.Exists(x => x.Round.Length == 0 && x.HousesOnTheRound == 1 && x.ValueOfTheRound == 8f),
            Describe(rounds));
    }

    /// <summary>
    /// how big a round is is a number of houses, so a house with two visits
    /// still outstanding is one house on it - but two jobs left to do
    /// </summary>
    private static void AHouseIsCountedOnceHoweverManyVisitsAreOut()
    {
        Console.WriteLine();
        Console.WriteLine("A house is one house however many visits of it are out");

        Reset();

        Job job = AddJob("7", "Mill Lane", 10f);
        job.SetFrequence(4, FrequenceType.Week);
        job.SetRound("Monday");

        //a second visit of the same house left outstanding alongside the
        //first, which is what a botched skip or a hand made copy leaves
        Job again = job.DeepCopy();
        again.DueDate = UsfulFuctions.DateNow;
        Job.Add(again);
        again.BaseJobId = job.BaseJobId;

        RoundStats stats = RoundStats.Now(12);

        Check("one house on the round", stats.HousesOnTheRound == 1, $"{stats.HousesOnTheRound} counted");
        Check("worth one visit of it", stats.ValueOfTheRound == 10f, $"{stats.ValueOfTheRound} counted");
        Check("but two jobs left to do", stats.HousesLeft == 2, $"{stats.HousesLeft} counted");
    }

    /// <summary>
    /// A month's takings came out short of the same days added up on the
    /// calendar page.
    ///
    /// The figures dropped every cancelled job before they looked at whether
    /// it had been done, so a house cleaned and then taken off the round took
    /// that clean's money out of the month it was earned in - and out of the
    /// income on the tax page with it. The calendar has always counted them.
    /// Cancelling says the house is not being cleaned any more; it does not
    /// say the clean never happened, and the customer was charged for it.
    /// </summary>
    private static void ACleanThatWasDoneCountsAfterTheJobIsCancelled()
    {
        Console.WriteLine();
        Console.WriteLine("A clean that was done still counts once the job is cancelled");

        Reset();

        Job job = AddJob("5", "Bridge Street", 12f);
        job.SetFrequence(4, FrequenceType.Week);

        //cleaned, and then the customer stopped. Cancelling is per visit, so
        //the whole job goes: the one that was done and the one it generated
        job.MarkJobDone(true);

        foreach (Job visit in job.EveryVisit())
            visit.CancelJob();

        //and one that is simply cancelled without ever being done, which is
        //work that is not going to happen and must not count as anything
        Job never = AddJob("7", "Bridge Street", 20f);
        never.CancelJob();

        RoundStats stats = RoundStats.Now(12);

        float takings = 0;
        int houses = 0;
        foreach (MonthOfWork m in stats.Months)
        {
            takings += m.Value;
            houses += m.Houses;
        }

        Check("the clean is still in the month's takings", takings == 12f, $"{takings} counted");
        Check("and counted as one house done", houses == 1, $"{houses} counted");

        //nothing outstanding is left at either house
        Check("neither house is on the round", stats.HousesOnTheRound == 0, $"{stats.HousesOnTheRound} counted");
        Check("and there is nothing left to do", stats.HousesLeft == 0, $"{stats.HousesLeft} counted");
    }

    /// <summary>
    /// a work list sent to somebody comes back readable: the header is in
    /// the clear, the wrong PIN opens nothing, the right one gives back what
    /// was sent, and what was ticked off travels with the return
    /// </summary>
    private static void ASharedWorkListSurvivesTheTripThereAndBack(string folder)
    {
        Console.WriteLine();
        Console.WriteLine("A shared work list survives the trip there and back");

        Reset();

        Job first = AddJob("12", "High Street", 10.50f);
        first.Notes = "Side gate sticks";
        Job second = AddJob("14", "High Street", 12f);

        SharedWorkData sent = WorkShare.BuildShare(new List<Job>() { first, second },
            prices: true, notes: true, phones: false, allowCollect: true, workerTag: "Dave");

        Check("both jobs went in", sent.Jobs.Count == 2, $"{sent.Jobs.Count} in the file");
        Check("the price was included", sent.Jobs[0].HasPrice && sent.Jobs[0].Price == 10.50f,
            $"{sent.Jobs[0].Price}");
        Check("the note was included", sent.Jobs[0].Notes == "Side gate sticks", sent.Jobs[0].Notes);
        Check("the phone number was not", sent.Jobs[0].Phone.Length == 0, sent.Jobs[0].Phone);

        string path = Path.Combine(folder, "test" + WorkShare.Extension);
        WorkShare.WriteFile(path, sent, "1234", WorkShareKind.SentWork);

        WorkShareHeader header = WorkShare.ReadHeader(path);
        Check("the header reads without the PIN", header != null && header.Key == sent.Key,
            header == null ? "no header" : header.Key);
        Check("and says which way the file is going",
            header != null && header.Kind == WorkShareKind.SentWork,
            header == null ? "no header" : header.Kind.ToString());

        Check("the wrong PIN opens nothing", WorkShare.ReadFile(path, "9999") == null, "it opened");

        SharedWorkData opened = WorkShare.ReadFile(path, "1234");
        Check("the right PIN opens it", opened != null && opened.Jobs.Count == 2,
            opened == null ? "did not open" : $"{opened.Jobs.Count} job(s)");

        if (opened == null)
            return;

        Check("the options travelled", opened.AllowCollect && opened.IncludePrices && !opened.IncludePhones,
            $"collect={opened.AllowCollect} prices={opened.IncludePrices} phones={opened.IncludePhones}");
        Check("the worker tag travelled", opened.WorkerTag == "Dave", opened.WorkerTag);

        //the worker marks the day off and the return carries it home
        opened.Jobs[0].Done = true;
        opened.Jobs[0].DoneOn = DateTime.Now.Date;
        opened.Jobs[0].Tags.Add("Front Only");
        opened.Jobs[1].Skipped = true;

        string returnPath = Path.Combine(folder, "return" + WorkShare.Extension);
        WorkShare.WriteFile(returnPath, opened, "1234", WorkShareKind.ReturnedWork);

        WorkShareHeader returnHeader = WorkShare.ReadHeader(returnPath);
        Check("the return still carries the same key in the clear",
            returnHeader != null && returnHeader.Key == sent.Key
            && returnHeader.Kind == WorkShareKind.ReturnedWork,
            returnHeader == null ? "no header" : $"{returnHeader.Key} {returnHeader.Kind}");

        SharedWorkData back = WorkShare.ReadFile(returnPath, "1234");
        Check("the work marked off came back", back != null && back.Jobs[0].Done
            && back.Jobs[0].Tags.Contains("Front Only") && back.Jobs[1].Skipped,
            back == null ? "did not open" : back.Jobs[0].FormattedStatus);
        Check("matched back to the sender's own job ids",
            back != null && back.Jobs[0].JobId == first.Id && back.Jobs[1].JobId == second.Id,
            back == null ? "did not open" : $"{back.Jobs[0].JobId}, {back.Jobs[1].JobId}");

        //the sender's copy is tagged so the round says the work is out -
        //quietly, so the tag picker is not offered every worker's name
        int knownTags = Job.TagNames.Count;
        first.AddTagQuietly(WorkShare.SentTag("Dave"));

        Check("the sent tag went on the job", first.HasTag("Sent To Dave"), first.TagsText);
        Check("without going on the list to pick from", Job.TagNames.Count == knownTags,
            $"{Job.TagNames.Count - knownTags} added");

        //work that is out is not offered for sending again
        Check("the job counts as out", WorkShare.IsOut(first), first.TagsText);
        Check("and one that was not sent does not", !WorkShare.IsOut(second), second.TagsText);

        first.RemoveTag(WorkShare.SentTag("Dave"));
        Check("and comes off when the work comes home", !first.HasTag("Sent To Dave"), first.TagsText);

        //a send with no return - the worker said at the gate what got done -
        //is cleared by name, off every job carrying the tag at once
        first.AddTagQuietly(WorkShare.SentTag("Dave"));
        second.AddTagQuietly(WorkShare.SentTag("Dave"));

        //the name is read back off the tags for the day headings and the
        //booking rows - each name once, however many jobs carry it
        List<string> outWith = WorkShare.OutWith(new List<Job>() { first, second });
        Check("the work says whose hands it is in", outWith.Count == 1 && outWith[0] == "Dave",
            string.Join(", ", outWith));

        int cleared = WorkShare.ClearSentTags("Dave");
        Check("clearing a send takes the tag off every job",
            cleared == 2 && !first.HasTag("Sent To Dave") && !second.HasTag("Sent To Dave"),
            $"{cleared} cleared, tags: '{first.TagsText}' '{second.TagsText}'");
    }

    /// <summary>
    /// cancelling work that was booked in takes it off the day. left booked,
    /// the work list kept a booking row counting work that every list
    /// filters out - a day with nothing behind it
    /// </summary>
    private static void CancellingBookedInWorkTakesItOffTheDay()
    {
        Console.WriteLine();
        Console.WriteLine("Cancelling booked in work takes it off the day");

        Reset();

        Job planned = AddJob("12", "High Street", 10f);
        planned.BookInJob(DateTime.Now.Date);
        planned.CancelJob();

        Check("the cancelled job is not booked in any more", !planned.IsBookedIn,
            $"still booked for {planned.DateJobBookinFor.ToShortDateString()}");

        //a clean already done stays on the day it was done - cancelling
        //stops the cleans to come, it does not unhappen that one
        Job done = AddJob("14", "High Street", 12f);
        done.BookInJob(DateTime.Now.Date);
        done.MarkJobDone(forceNotSave: true);
        done.CancelJob();

        Check("a done job keeps the day it was done on", done.IsBookedIn, "was unbooked");
    }

    /// <summary>
    /// debt written off leaves a record - who, when, how much and why - and
    /// the record survives a save and load. the record is the point: it is
    /// the line that makes the history add up when a customer argues
    /// </summary>
    private static void AWriteOffLeavesARecord()
    {
        Console.WriteLine();
        Console.WriteLine("A write off leaves a record");

        Reset();
        BalanceAdjustment.DeleteData();

        Customer customer = new Customer("12", "High Street");
        Customer.Add(customer);

        Job j = AddJob("12", "High Street", 10f);
        j.CustomerId = customer.Id;
        j.MarkJobDone(forceNotSave: true);

        Check("the clean put its price on the balance", customer.Balance == 10f, $"{customer.Balance}");

        j.SettleBalance("gate was locked all month");

        Check("settling cleared the balance", customer.Balance == 0, $"{customer.Balance}");

        List<BalanceAdjustment> records = BalanceAdjustment.ForCustomer(customer.Id);
        Check("the write off left a record", records.Count == 1 && records[0].Amount == 10f,
            records.Count == 0 ? "no record" : $"{records.Count} record(s), first {records[0].Amount}");
        Check("with the reason on it",
            records.Count == 1 && records[0].Reason == "gate was locked all month",
            records.Count == 0 ? "no record" : records[0].Reason);

        //thrown away and read back off the file the settle wrote
        BalanceAdjustment.DeleteData();
        BalanceAdjustment.Load();

        records = BalanceAdjustment.ForCustomer(customer.Id);
        Check("and the record survives a save and load",
            records.Count == 1 && records[0].Reason == "gate was locked all month"
            && records[0].Kind == BalanceAdjustmentKind.WriteOff,
            records.Count == 0 ? "no record" : records[0].Describe);
    }

    private static string Describe(List<RoundStats> rounds)
    {
        List<string> said = new List<string>();
        foreach (RoundStats r in rounds)
            said.Add($"{r.RoundName}={r.HousesOnTheRound} house(s)");

        return said.Count == 0 ? "no rounds" : string.Join(", ", said);
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

    /// <summary>
    /// two accounts keep two layouts, the layout out of an old settings file
    /// becomes the first account, and an expense recorded before accounts
    /// existed is still recognised - on the account that inherited it and on
    /// no other
    /// </summary>
    private static void EachBankAccountKeepsItsOwnLayoutAndItsOwnReferences(string folder)
    {
        Console.WriteLine();
        Console.WriteLine("Each bank account keeps its own layout and its own references");

        BankAccount.DeleteData();
        Expense.DeleteData();

        //two accounts, two layouts, through a save and back
        BankAccount starling = BankAccount.Add("Starling");
        starling.Date = 0;
        starling.Ref = 1;
        starling.Amount = 2;
        starling.Debit = 3;

        BankAccount hsbc = BankAccount.Add("HSBC");
        hsbc.Date = 2;
        hsbc.Ref = 4;
        hsbc.Amount = 5;
        hsbc.DebitAndCreditTogether = true;
        hsbc.Archived = true;

        BankAccount.Save(folder);
        BankAccount.DeleteData();
        BankAccount.Load(folder);

        Check("both accounts came back", BankAccount.Count == 2, $"{BankAccount.Count} came back");

        BankAccount readStarling = BankAccount.Query().Find(x => x.Name == "Starling");
        BankAccount readHsbc = BankAccount.Query().Find(x => x.Name == "HSBC");

        Check("the two accounts have their own ids",
            readStarling != null && readHsbc != null && readStarling.Id != readHsbc.Id,
            "an id was lost or shared");
        Check("the first account's layout survived",
            readStarling != null && readStarling.Ref == 1 && readStarling.Debit == 3,
            readStarling == null ? "account missing" : $"ref {readStarling.Ref}, debit {readStarling.Debit}");
        Check("the second account's layout survived",
            readHsbc != null && readHsbc.Amount == 5 && readHsbc.DebitAndCreditTogether,
            readHsbc == null ? "account missing" : $"amount {readHsbc.Amount}");
        Check("the archive survived the save",
            readHsbc != null && readHsbc.Archived, "came back in use");
        Check("an archived account is not offered for imports",
            BankAccount.QueryActive().Count == 1 && BankAccount.QueryActive()[0].Name == "Starling",
            $"{BankAccount.QueryActive().Count} offered");

        //the headings an account's statements are recognised by
        string heading = BankAccount.SignatureOf(new[] { "Date", " Description", "Amount" });
        Check("headings boil down the same however they are written",
            heading == BankAccount.SignatureOf(new[] { "date", "description ", "AMOUNT" }), "they differ");
        Check("no headings is no signature",
            BankAccount.SignatureOf(null) == string.Empty
                && BankAccount.FindBySignature(string.Empty, false) == null,
            "a blank matched something");

        readStarling.RememberSignature(false, heading);
        Check("the account whose statements look like this is guessed",
            BankAccount.FindBySignature(heading, false) == readStarling, "not found");

        //the archived account sharing the look does not muddle the guess -
        //it is not offered, so it is not guessed either
        readHsbc.RememberSignature(false, heading);
        Check("an archived account does not muddle the guess",
            BankAccount.FindBySignature(heading, false) == readStarling, "it did");

        //back in use, two accounts at the same bank look the same, and
        //guessing between them would guess wrong half the time
        readHsbc.Archived = false;
        Check("two accounts at the same bank are not guessed between",
            BankAccount.FindBySignature(heading, false) == null, "one was guessed");

        //the layout out of an old settings file becomes the first account
        BankAccount.DeleteData();
        BankAccount.LegacyDate = 1;
        BankAccount.LegacyRef = 2;
        BankAccount.LegacyAmount = 3;
        BankAccount.LegacyDebit = 4;

        string migrationFolder = Path.Combine(folder, "migration");
        Directory.CreateDirectory(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), migrationFolder));
        BankAccount.Load(migrationFolder);

        BankAccount migrated = BankAccount.Count == 1 ? BankAccount.Query()[0] : null;
        Check("the old layout became the one account", migrated != null, $"{BankAccount.Count} account(s)");
        Check("with the columns that were taught",
            migrated != null && migrated.Date == 1 && migrated.Ref == 2 && migrated.Amount == 3 && migrated.Debit == 4,
            migrated == null ? "no account" : $"date {migrated.Date}, ref {migrated.Ref}");
        Check("and it inherits the old references",
            migrated != null && migrated.InheritsLegacyReferences, "flag not set");

        //0,0,0 is a settings file from before imports existed, not a layout
        BankAccount.DeleteData();
        BankAccount.LegacyDate = 0;
        BankAccount.LegacyRef = 0;
        BankAccount.LegacyAmount = 0;
        BankAccount.LegacyPdfDate = -1;
        BankAccount.LegacyPdfRef = -1;
        BankAccount.LegacyPdfAmount = -1;

        string emptyFolder = Path.Combine(folder, "empty");
        Directory.CreateDirectory(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), emptyFolder));
        BankAccount.Load(emptyFolder);

        Check("a settings file with no layout makes no account", BankAccount.Count == 0, $"{BankAccount.Count} made");

        //an expense recorded before accounts existed is recognised by the
        //account that inherited it, and by no other account - the same
        //transaction on another account is another transaction
        BankAccount inheritor = BankAccount.Add("My Bank");
        inheritor.InheritsLegacyReferences = true;
        BankAccount other = BankAccount.Add("Second Account");

        DateTime day = new DateTime(2026, 5, 10);

        Expense old = new Expense()
        {
            Date = day,
            Amount = 25.50f,
            Merchant = "Garage",
            ExternalReference = Expense.StatementReference(day, "GARAGE FUEL", 25.50f, 0),
        };
        Expense.Add(old);

        Check("the inheriting account still finds it",
            Expense.FindFromStatement(inheritor, day, "GARAGE FUEL", 25.50f, 0) == old, "not found");
        Check("another account does not",
            Expense.FindFromStatement(other, day, "GARAGE FUEL", 25.50f, 0) == null, "matched across accounts");

        //a new expense carries its account in the reference and is found
        //through it
        Expense fresh = new Expense()
        {
            Date = day,
            Amount = 12f,
            Merchant = "Ladder Shop",
            ExternalReference = Expense.StatementReference(other, day, "LADDER SHOP", 12f, 0),
        };
        Expense.Add(fresh);

        Check("an account-tagged reference is found on its account",
            Expense.FindFromStatement(other, day, "LADDER SHOP", 12f, 0) == fresh, "not found");
        Check("and not on a different account",
            Expense.FindFromStatement(inheritor, day, "LADDER SHOP", 12f, 0) == null, "matched across accounts");

        //no account - a PayPal export - is the plain reference, unchanged
        Check("no account means the reference from before",
            Expense.StatementReference(null, day, "GARAGE FUEL", 25.50f, 0)
                == Expense.StatementReference(day, "GARAGE FUEL", 25.50f, 0),
            "the two differ");

        //leave nothing behind for whatever runs next
        BankAccount.DeleteData();
        Expense.DeleteData();
        BankAccount.LegacyDate = -1;
        BankAccount.LegacyRef = -1;
        BankAccount.LegacyAmount = -1;
        BankAccount.LegacyDebit = -1;
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

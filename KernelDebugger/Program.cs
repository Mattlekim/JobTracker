using Kernel;


void ListCustomers(List<Customer> customers)
{

    foreach (Customer c in customers)
        Console.WriteLine($"Customer> ID: {c.Id} Name: {c.FName} {c.SName} Address: {c.Address.PropertyNameNumber} {c.Address.Street}");
}


void ListCustomersWithJobs(List<Customer> customers)
{

    List<Job> _jobs;
    int i = 0;
    foreach (Customer c in customers)
    {
        _jobs = Job.Query(QueryType.CustomerId, c.Id);
        Console.WriteLine($"Customer> ID: {c.Id} Name: {c.FName} {c.SName} Address: {c.Address.PropertyNameNumber} {c.Address.Street} >>> JOBS : {_jobs.Count}");
        if (_jobs.Count > 0)
        {
            foreach(Job j in _jobs)
            Console.WriteLine($"JobId: {j.Id} Price: {j.Price} Freqeunce {j.Frequence}");
                //i++;
        }
    }
}

void ListDueJobs()
{

    List<Job> _jobs;
    int i = 0;


    _jobs = Job.FindJobs(QueryType.BeforeDate, DateTime.Now, JobFilter.NotCompleated);

    Console.WriteLine($"Due Jobs ====");
    foreach (Job j in _jobs)
        Console.WriteLine($"JobId: {j.Id} Price: {j.Price} Freqeunce {j.Frequence} DueDate {j.DueDate}");

}


void ListAmountOwed(List<Customer> customers)
{

    List<Job> _jobs;
    int i = 0;
    foreach (Customer c in customers)
    {
        _jobs = Job.Query(QueryType.CustomerId, c.Id);
        _jobs = _jobs.FindAll(x => x.IsCompleted);
        Console.WriteLine($"Customer> ID: {c.Id} Name: {c.FName} {c.SName} Address: {c.Address.PropertyNameNumber} {c.Address.Street} >>> JOBS : {_jobs.Count}");

        Console.WriteLine($"Customer Owes : £{c.Balance}");

    }

}
List<Customer> customers;

Console.Clear();
Console.WriteLine("Debbuger Starting");
Console.WriteLine("Debbuger Ready");

Customer.Load();
/*
Customer.Add("32", "Pit Street");
Customer.Add("34", "Pit Street");
Customer.Add("2", "King Street");
Customer.Add("37", "Pit Street");
*/


DateTime dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day - 3);


//Job.Load();
Payment.Load();
//Job.Add(0, 5.50f, 0);
//Job.Add(3, 6.50f, 4);


Filter filter = new Filter("street", "pit", false); //a filter for the customers

customers = Customer.Query(filter); //query the customers

ListCustomers(customers);




Console.WriteLine("");

ListCustomersWithJobs(customers);

ListDueJobs();
//Customer.ListQueryDebug(); //output the query to the screen

//Payment.Add(90, PaymentMethod.Bank, "s83fv");



Customer.CalculateCustomerBill();
ListAmountOwed(customers);

Customer.Save();
Job.Save();
Payment.Save();
Console.ReadLine();




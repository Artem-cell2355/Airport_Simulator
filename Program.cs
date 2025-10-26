using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AirportSim
{
    class Passenger
    {
        public string Name { get; set; }
        public string FlightNumber { get; set; }
        public bool HasTicket { get; set; }
        public bool PassedSecurity { get; set; }
        public bool IsOnBoard { get; set; }
    }

    class Flight
    {
        public string FlightNumber { get; set; }
        public string Destination { get; set; }
        public int DepartureTime { get; set; }       // у тиках
        public string Status { get; set; } = "OnTime";
        public int Capacity { get; set; }
        public int BoardingStartTime => DepartureTime - 2; // посадка за 2 тики до вильоту
        public List<Passenger> PassengersOnBoard { get; } = new List<Passenger>();
    }

    class Airport
    {
        //параметри симуляції
        private const int REG_COUNTERS = 3;
        private const int SEC_CHECKS = 2;
        private const int BOARDING_RATE = 5;
        private const double NEW_PASSENGER_PROB = 0.6; // імовірність появи нових пасажирів за тік
        private readonly Random rnd = new Random();

        //дані симуляції
        public int CurrentTime { get; private set; } = 0;
        public List<Flight> Flights { get; } = new List<Flight>();
        public List<Passenger> AllPassengers { get; } = new List<Passenger>();
        public Queue<Passenger> RegistrationQueue { get; } = new Queue<Passenger>();
        public Queue<Passenger> SecurityQueue { get; } = new Queue<Passenger>();

        private int idCounter = 1;
        private readonly string[] firstNames = { "Dmytro", "Anna", "Mark", "Maria", "Sergiy", "Oleh", "Iryna", "Taras", "Olena", "Nazar" };
        private readonly string[] lastNames = { "Shevchenko", "Koval", "Melnyk", "Bondar", "Tkachenko", "Ivanenko", "Petrenko", "Savchenko" };

        private Passenger CreateRandomPassenger()
        {
            if (Flights.Count == 0) return null;
            var flight = Flights[rnd.Next(Flights.Count)];
            string name = $"{firstNames[rnd.Next(firstNames.Length)]} {lastNames[rnd.Next(lastNames.Length)]} #{idCounter++}";
            return new Passenger { Name = name, FlightNumber = flight.FlightNumber };
        }

        public void MaybeSpawnPassengers(List<string> events)
        {
            if (rnd.NextDouble() <= NEW_PASSENGER_PROB)
            {
                int count = rnd.Next(1, 3);
                for (int i = 0; i < count; i++)
                {
                    var p = CreateRandomPassenger();
                    if (p != null)
                    {
                        AllPassengers.Add(p);
                        RegistrationQueue.Enqueue(p);
                        events.Add($"Новий пасажир: {p.Name} -> рейс {p.FlightNumber}. Додано до черги на реєстрацію.");
                    }
                }
            }
        }

        public void ProcessRegistration(List<string> events)
        {
            int toServe = Math.Min(REG_COUNTERS, RegistrationQueue.Count);
            for (int i = 0; i < toServe; i++)
            {
                var p = RegistrationQueue.Dequeue();
                //перевірка на існування рейсу
                var flightExists = Flights.Any(f => f.FlightNumber == p.FlightNumber);
                if (flightExists)
                {
                    p.HasTicket = true;
                    SecurityQueue.Enqueue(p);
                    events.Add($"{p.Name} зареєстрований(на) на рейс {p.FlightNumber} і перейшов(ла) на контроль.");
                }
                else
                {
                    //якщо рейсу немає
                    events.Add($"{p.Name}: рейсу {p.FlightNumber} немає — пасажир очікує перенаправлення.");
                }
            }
        }

        public void ProcessSecurity(List<string> events)
        {
            int toServe = Math.Min(SEC_CHECKS, SecurityQueue.Count);
            for (int i = 0; i < toServe; i++)
            {
                var p = SecurityQueue.Dequeue();
                p.PassedSecurity = true;
                events.Add($"{p.Name} пройшов(ла) контроль безпеки.");
            }
        }

        public void UpdateFlightsAndBoarding(List<string> events)
        {
            foreach (var f in Flights)
            {
                //початок посадки
                if (CurrentTime >= f.BoardingStartTime && CurrentTime < f.DepartureTime && f.Status == "OnTime")
                {
                    f.Status = "Boarding";
                    events.Add($"Почалася посадка на рейс {f.FlightNumber} до {f.Destination}.");
                }

                //посадка під час Boarding
                if (f.Status == "Boarding" && CurrentTime < f.DepartureTime)
                {
                    var ready = AllPassengers.Where(p =>
                        p.FlightNumber == f.FlightNumber &&
                        p.HasTicket &&
                        p.PassedSecurity &&
                        !p.IsOnBoard).Take(BOARDING_RATE).ToList();

                    foreach (var p in ready)
                    {
                        if (f.PassengersOnBoard.Count >= f.Capacity) break;
                        p.IsOnBoard = true;
                        f.PassengersOnBoard.Add(p);
                        events.Add($"{p.Name} здійснив(ла) посадку на рейс {f.FlightNumber}.");
                    }
                }

                // Виліт
                if (CurrentTime >= f.DepartureTime && f.Status != "Departed")
                {
                    f.Status = "Departed";
                    // Хто не встиг сісти — запізнився
                    var late = AllPassengers.Where(p => p.FlightNumber == f.FlightNumber && !p.IsOnBoard).ToList();
                    foreach (var p in late)
                        events.Add($"Пасажир {p.Name} запізнився на рейс {f.FlightNumber}!");

                    events.Add($"Рейс {f.FlightNumber} вилетів до {f.Destination}. На борту: {f.PassengersOnBoard.Count}/{f.Capacity}.");
                }
            }
            var departedFlights = Flights.Where(f => f.Status == "Departed").ToList();
            foreach (var f in departedFlights)
            {
                //видалити пасажирів які полетіли
                foreach (var p in f.PassengersOnBoard)
                    AllPassengers.Remove(p);

                //видалити пасажирів які чекали на цей рейс
                var regRemain = RegistrationQueue.Where(p => p.FlightNumber != f.FlightNumber).ToList();
                RegistrationQueue.Clear();
                foreach (var p in regRemain) RegistrationQueue.Enqueue(p);

                var secRemain = SecurityQueue.Where(p => p.FlightNumber != f.FlightNumber).ToList();
                SecurityQueue.Clear();
                foreach (var p in secRemain) SecurityQueue.Enqueue(p);
            }
            Flights.RemoveAll(f => f.Status == "Departed");
        }

        public int WaitingAtGateCount()
        {
            //пройшли безпеку, мають квиток, не на борту, рейс активний
            var activeFlights = new HashSet<string>(Flights.Select(f => f.FlightNumber));
            return AllPassengers.Count(p => p.HasTicket && p.PassedSecurity && !p.IsOnBoard && activeFlights.Contains(p.FlightNumber));
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }

        private static ConsoleColor StatusColor(string status) => status switch
        {
            "OnTime" => ConsoleColor.Gray,
            "Delayed" => ConsoleColor.Yellow,
            "Boarding" => ConsoleColor.Cyan,
            "Departed" => ConsoleColor.Green,
            _ => ConsoleColor.White
        };

        public void PrintStatus(List<string> events)
        {
            Console.WriteLine($"\n=== Час {CurrentTime} ===");

            if (Flights.Count == 0)
            {
                WriteColored("Немає активних рейсів.", ConsoleColor.DarkGray);
            }
            else
            {
                foreach (var f in Flights.OrderBy(f => f.DepartureTime))
                {
                    var line = $"Flight {f.FlightNumber} -> {f.Destination} | {f.Status} | Dep @ {f.DepartureTime} | OnBoard {f.PassengersOnBoard.Count}/{f.Capacity}";
                    WriteColored(line, StatusColor(f.Status));
                }
            }

            Console.WriteLine($"Черга на реєстрацію: {RegistrationQueue.Count}");
            Console.WriteLine($"Черга на контроль:   {SecurityQueue.Count}");
            Console.WriteLine($"У зоні вильоту (чекають посадки): {WaitingAtGateCount()}");

            if (events.Count > 0)
            {
                WriteColored("\nПодії:", ConsoleColor.Magenta);
                foreach (var e in events) Console.WriteLine("• " + e);
            }
        }

        public void Tick()
        {
            CurrentTime++;
            var events = new List<string>();
            MaybeSpawnPassengers(events);
            ProcessRegistration(events);
            ProcessSecurity(events);
            UpdateFlightsAndBoarding(events);
            PrintStatus(events);
        }
    }

    class Program
    {
        // Постав 0, щоб запускати без обмеження (зупинка — натисни Q)
        private const int TICKS = 0;
        private const int TICK_DELAY_MS = 400;

        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var airport = new Airport();

            // Приклади рейсів
            airport.Flights.Add(new Flight { FlightNumber = "PS101", Destination = "Kyiv", DepartureTime = 8, Capacity = 6 });
            airport.Flights.Add(new Flight { FlightNumber = "PS202", Destination = "Lviv", DepartureTime = 10, Capacity = 4 });
            airport.Flights.Add(new Flight { FlightNumber = "PS303", Destination = "Odesa", DepartureTime = 12, Capacity = 5 });

            if (TICKS > 0)
            {
                for (int i = 0; i < TICKS; i++)
                {
                    airport.Tick();
                    Thread.Sleep(TICK_DELAY_MS);
                }
                Console.WriteLine("\nСимуляцію завершено (10 тіків).");
            }
            else
            {
                Console.WriteLine("Нескінченний режим. Натисни 'Q' щоб вийти.");
                while (true)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) break;
                    airport.Tick();
                    Thread.Sleep(TICK_DELAY_MS);
                }
            }
        }
    }
}


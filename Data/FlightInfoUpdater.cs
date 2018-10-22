using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectFlight.Data
{
	/// <summary>
	/// Updates the flight info database in the background
	/// </summary>
	public class FlightInfoUpdater : IDisposable
	{
		/// <summary>
		/// When the initial flight infos gets replaced
		/// </summary>
		/// <param name="amount">Amount of flights that got added</param>
		public delegate void AddEvent(int amount);

		/// <summary>
		/// When the flight infos gets updated
		/// </summary>
		/// <param name="amount">Amount of flights that got updated</param>
		public delegate void RefreshEvent(int amount);

		/// <summary>
		/// When the initial flight infos gets replaced
		/// </summary>
		public event AddEvent OnAdd;

		/// <summary>
		/// When the flight infos gets updated
		/// </summary>
		public event RefreshEvent OnRefresh;

        /// <summary>
        /// The limit of entries to add to the database
        /// </summary>
        private readonly int limit;

		/// <summary>
		/// Delay between each refresh
		/// </summary>
		private readonly TimeSpan delay;

		/// <summary>
		/// Keep running the refresh loop
		/// </summary>
		private bool running;

        /// <summary>
        /// Fetch new flight infos from the API
        /// </summary>
        private static IEnumerable<FlightInfoResponse> FlightInfoResponses
        {
            get
            {
                // Fetch response from API server
                string response;
                using (var client = new WebClient())
                    response = client.DownloadString("https://public-api.adsbexchange.com/VirtualRadar/AircraftList.json");
                    
                // Parse the response
                dynamic json = JsonConvert.DeserializeObject(response);
                return json.acList.ToObject<IEnumerable<FlightInfoResponse>>();
            }
        }

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="planeLimit">Limit on how many entries to save to the database</param>
		/// <param name="refreshDelay">The delay between each refresh</param>
		public FlightInfoUpdater(int planeLimit, TimeSpan refreshDelay)
        {
            limit = planeLimit;
	        delay = refreshDelay;
            Task.Run(() => UpdateFlightInfos());
        }
		
		/// <summary>
		/// Stop the refresh loop
		/// </summary>
		public void Dispose() => running = false;

		/// <summary>
		/// Starts updating flight info on the current thread
		/// </summary>
		private void UpdateFlightInfos()
		{
            // First overwrite all current entries
            Overwrite();

            // Start refresh loop
			running = true;
            while (running)
            {
                Refresh();
				Thread.Sleep(delay);
            }
	    }

        /// <summary>
        /// Removes all current entries and refreshes with new ones
        /// </summary>
        private void Overwrite()
        {
            // TODO: Convert the response to FlightInfo
            var infos = new List<FlightInfo>();
            foreach (var flight in FlightInfoResponses)
		    {
                // Check if we've added more than our limit
                if (infos.Count >= limit)
                    continue;

                // Add FlightInfo to list
                var info = new FlightInfo(flight);
                infos.Add(info);
			}

			// Save to database and overwrite current entries
			WriteChanges(infos, true);

			// Trigger OnAdd event
			OnAdd?.Invoke(infos.Count);
		}

        /// <summary>
        /// Updates the current entries
        /// </summary>
        private void Refresh()
        {
            // First get the new flight info
			// We convert it to a dictionary to search in it faster
            var newFlights = FlightInfoResponses.ToDictionary(f => f.Icao);

			// Changes made to the database
	        int changes;

            using (var context = new ApplicationDbContext())
            {
				// Loop through all existing planes and try to update them

	            foreach (var info in context.FlightInfos)
	            {
		            // Try to find it in the list of all flights
		            if (newFlights.ContainsKey(info.Id))
		            {
						// Get the new value
			            var updated = newFlights[info.Id];

						// For now at least, only update position
			            info.Latitude  = updated.Lat;
			            info.Longitude = updated.Long;
		            }
	            }

				// Update database
	            changes = context.SaveChanges();
            }

			// Trigger OnRefresh event
			OnRefresh?.Invoke(changes);
        }

        /// <summary>
        /// Writes changes to the database
        /// </summary>
        /// <param name="infos">The info to save to the database</param>
        /// <param name="emptyTable">Empty the table before adding values</param>
        // TODO: Remove this?
        private static void WriteChanges(IEnumerable<FlightInfo> infos, bool emptyTable = false)
        {
            using (var context = new ApplicationDbContext())
            {
                // See if we should empty it first
                if (emptyTable)
                    context.FlightInfos.RemoveRange(context.FlightInfos);

                // Add infos from list to db set
                foreach (var info in infos)
                {
                    // Database type time can't store values higher than 24 hours
                    if (info.Tracked.TotalHours >= 24)
                        info.Tracked = TimeSpan.Parse("23:59:59.9999999");

					// Make sure it can be added to the database
	                var errors = info.Validate();
					// TODO: For now, throw an exception
	                if (errors.Any())
		                throw new InvalidOperationException($"Can't add {info.Id} due to invalid fields");

	                context.FlightInfos.Add(info);
                }

				context.SaveChanges();
			}
        }
    }
}
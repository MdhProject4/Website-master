﻿using Microsoft.EntityFrameworkCore;

namespace ProjectFlight.Data
{
	/// <summary>
	/// Representation of our database
	/// </summary>
	public class ApplicationDbContext : DbContext
	{
		#region Properties

		public static string ConnectionString { private get; set; }

		#endregion

		#region Database tables

		public DbSet<FlightInfo> FlightInfos { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<FlightBookmark> FlightBookmarks { get; set; }

		#endregion

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			base.OnConfiguring(optionsBuilder);
			optionsBuilder.UseSqlServer(ConnectionString);
		}
	}
}
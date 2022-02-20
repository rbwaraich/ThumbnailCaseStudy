#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DemoClassLibrary;

    public class DemoDbContext : DbContext
    {
        public DemoDbContext (DbContextOptions<DemoDbContext> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employee { get; set; }
    }

using Microsoft.EntityFrameworkCore;

namespace QueryProjection.Tests;

public class EFCoreTests : IDisposable
{
    private AppContext _context;

    private const string FirstName = "John";
    private const string LastName = "Doe";
    private const int Age = 21;
    private const string FavouriteSnack = "Cookies";
    private const string FavouriteAnimal = "Lion";

    public EFCoreTests()
    {
        _context = new AppContext();
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        var idCard = new IdCard()
        {
            FirstName = FirstName,
            LastName = LastName,
            Age = Age
        };
        _context.People.Add(new Person()
        {
            FavouriteSnack = FavouriteSnack,
            FavouriteAnimal = FavouriteAnimal,
            IdCard = idCard
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void CanProject_OneLayer_Successfully()
    {
        var fromToMapping = new List<IMapping<Person>>()
        {
            new FromToMapping<Person>(to: "FavSnack", from: "FavouriteSnack"),
            new CustomMapping<Person, bool>(to: "HasLionAsAnimal", from: x => x.FavouriteAnimal.Contains(FavouriteAnimal))
        };

        var query = _context.People.Project(fromToMapping);
        var queryString = query.ToQueryString();
        Assert.DoesNotContain(nameof(Person.IdCard), queryString);
        Assert.Contains(nameof(Person.FavouriteSnack), queryString);
        Assert.Contains(nameof(Person.FavouriteAnimal), queryString);

        var result = query.First();
        var fields = result.GetType().GetFields();
        Assert.Equal(2, fields.Length);

        var field = fields.FirstOrDefault(x => x.Name == "FavSnack");
        Assert.NotNull(field);
        Assert.Equal(FavouriteSnack, field.GetValue(result));

        field = fields.FirstOrDefault(x => x.Name == "HasLionAsAnimal");
        Assert.NotNull(field);
        Assert.Equal(true, field.GetValue(result));
    }

    [Fact]
    public void CanProject_TwoLayers_Successfully()
    {
        var fromToMapping = new List<IMapping<Person>>()
        {
            new FromToMapping<Person>(to: "FirstName" , from: "IdCard.FirstName"),
            new CustomMapping<Person, bool>(to: "HasJohnOnIdCard", from: x => x.IdCard.FirstName.Contains(FirstName))
        };

        var query = _context.People.Project(fromToMapping);
        var queryString = query.ToQueryString();
        Assert.Contains(nameof(Person.IdCard), queryString);
        Assert.DoesNotContain(nameof(Person.FavouriteAnimal), queryString);
        Assert.DoesNotContain(nameof(Person.FavouriteSnack), queryString);

        var result = query.First();
        var fields = result.GetType().GetFields();
        Assert.Equal(2, fields.Length);

        var field = fields.FirstOrDefault(x => x.Name == "FirstName");
        Assert.NotNull(field);
        Assert.Equal(FirstName, field.GetValue(result));

        field = fields.FirstOrDefault(x => x.Name == "HasJohnOnIdCard");
        Assert.NotNull(field);
        Assert.Equal(true, field.GetValue(result));
    }
}

public class AppContext : DbContext
{
    public DbSet<Person> People { get; set; }
    public DbSet<IdCard> IdCards { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source=QueryProjection.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>()
            .HasOne(x => x.IdCard)
            .WithOne(x => x.Person)
            .HasForeignKey<Person>(x => x.IdCardId);
    }
}

public class Person
{
    public int Id { get; set; }
    public string FavouriteSnack { get; set; } = null!;
    public string FavouriteAnimal { get; set; } = null!;
    public IdCard IdCard { get; set; } = null!;
    public int IdCardId { get; set; }
}

public class IdCard
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public int Age { get; set; }
    public Person Person { get; set; } = null!;
    public int PersonId { get; set; }
}
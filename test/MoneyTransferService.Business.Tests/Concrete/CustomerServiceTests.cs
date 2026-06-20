using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MoneyTransferService.Business.Concrete;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Tests.Concrete;

public class CustomerServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<IndividualCustomer>> _individualCustomerRepositoryMock;
    private readonly Mock<IRepository<CorporateCustomer>> _corporateCustomerRepositoryMock;
    private readonly CustomerService _customerService;

    public CustomerServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _individualCustomerRepositoryMock = new Mock<IRepository<IndividualCustomer>>();
        _corporateCustomerRepositoryMock = new Mock<IRepository<CorporateCustomer>>();

        _unitOfWorkMock.Setup(u => u.GetRepository<IndividualCustomer>()).Returns(_individualCustomerRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.GetRepository<CorporateCustomer>()).Returns(_corporateCustomerRepositoryMock.Object);

        _customerService = new CustomerService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task CreateIndividualCustomerAsync_ShouldCreateCustomer_AndSaveChanges()
    {
        // Arrange
        var email = "individual@example.com";
        var phoneNumber = "+905551112233";
        var firstName = "Ada";
        var lastName = "Lovelace";
        var nationalIdentityNumber = "12345678901";

        _individualCustomerRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<IndividualCustomer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _customerService.CreateIndividualCustomerAsync(
            email,
            phoneNumber,
            firstName,
            lastName,
            nationalIdentityNumber);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(email);
        result.PhoneNumber.Should().Be(phoneNumber);
        result.FirstName.Should().Be(firstName);
        result.LastName.Should().Be(lastName);
        result.NationalIdentityNumber.Should().Be(nationalIdentityNumber);

        _individualCustomerRepositoryMock.Verify(r => r.AddAsync(
            It.Is<IndividualCustomer>(c =>
                c.Email == email &&
                c.PhoneNumber == phoneNumber &&
                c.FirstName == firstName &&
                c.LastName == lastName &&
                c.NationalIdentityNumber == nationalIdentityNumber),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCorporateCustomerAsync_ShouldCreateCustomer_AndSaveChanges()
    {
        // Arrange
        var email = "corp@example.com";
        var phoneNumber = "+905559998877";
        var companyName = "Contoso Ltd";
        var taxNumber = "1234567890";
        var taxOffice = "Istanbul";

        _corporateCustomerRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<CorporateCustomer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _customerService.CreateCorporateCustomerAsync(
            email,
            phoneNumber,
            companyName,
            taxNumber,
            taxOffice);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(email);
        result.PhoneNumber.Should().Be(phoneNumber);
        result.CompanyName.Should().Be(companyName);
        result.TaxNumber.Should().Be(taxNumber);
        result.TaxOffice.Should().Be(taxOffice);

        _corporateCustomerRepositoryMock.Verify(r => r.AddAsync(
            It.Is<CorporateCustomer>(c =>
                c.Email == email &&
                c.PhoneNumber == phoneNumber &&
                c.CompanyName == companyName &&
                c.TaxNumber == taxNumber &&
                c.TaxOffice == taxOffice),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, "+905551112233", "Ada", "Lovelace", "12345678901")]
    [InlineData("individual@example.com", null, "Ada", "Lovelace", "12345678901")]
    [InlineData("individual@example.com", "+905551112233", null, "Lovelace", "12345678901")]
    [InlineData("individual@example.com", "+905551112233", "Ada", null, "12345678901")]
    [InlineData("individual@example.com", "+905551112233", "Ada", "Lovelace", null)]
    public async Task CreateIndividualCustomerAsync_ShouldThrowInvalidCustomerRequestException_WhenRequiredFieldsAreInvalid(
        string? email,
        string? phoneNumber,
        string? firstName,
        string? lastName,
        string? nationalIdentityNumber)
    {
        // Act
        Func<Task> act = async () => await _customerService.CreateIndividualCustomerAsync(
            email!,
            phoneNumber!,
            firstName!,
            lastName!,
            nationalIdentityNumber!);

        // Assert
        await act.Should().ThrowAsync<InvalidCustomerRequestException>();
        _individualCustomerRepositoryMock.Verify(r => r.AddAsync(It.IsAny<IndividualCustomer>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "+905559998877", "Contoso Ltd", "1234567890", "Istanbul")]
    [InlineData("corp@example.com", null, "Contoso Ltd", "1234567890", "Istanbul")]
    [InlineData("corp@example.com", "+905559998877", null, "1234567890", "Istanbul")]
    [InlineData("corp@example.com", "+905559998877", "Contoso Ltd", null, "Istanbul")]
    [InlineData("corp@example.com", "+905559998877", "Contoso Ltd", "1234567890", null)]
    public async Task CreateCorporateCustomerAsync_ShouldThrowInvalidCustomerRequestException_WhenRequiredFieldsAreInvalid(
        string? email,
        string? phoneNumber,
        string? companyName,
        string? taxNumber,
        string? taxOffice)
    {
        // Act
        Func<Task> act = async () => await _customerService.CreateCorporateCustomerAsync(
            email!,
            phoneNumber!,
            companyName!,
            taxNumber!,
            taxOffice!);

        // Assert
        await act.Should().ThrowAsync<InvalidCustomerRequestException>();
        _corporateCustomerRepositoryMock.Verify(r => r.AddAsync(It.IsAny<CorporateCustomer>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateIndividualCustomerAsync_ShouldTranslateDbUpdateException_ToCustomerCreationException()
    {
        // Arrange
        _individualCustomerRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<IndividualCustomer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("db failure"));

        // Act
        Func<Task> act = async () => await _customerService.CreateIndividualCustomerAsync(
            "individual@example.com",
            "+905551112233",
            "Ada",
            "Lovelace",
            "12345678901");

        // Assert
        var exception = await act.Should().ThrowAsync<CustomerCreationException>();
        exception.Which.InnerException.Should().BeOfType<DbUpdateException>();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCorporateCustomerAsync_ShouldTranslateDbUpdateException_ToCustomerCreationException()
    {
        // Arrange
        _corporateCustomerRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<CorporateCustomer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("db failure"));

        // Act
        Func<Task> act = async () => await _customerService.CreateCorporateCustomerAsync(
            "corp@example.com",
            "+905559998877",
            "Contoso Ltd",
            "1234567890",
            "Istanbul");

        // Assert
        var exception = await act.Should().ThrowAsync<CustomerCreationException>();
        exception.Which.InnerException.Should().BeOfType<DbUpdateException>();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetIndividualCustomerByIdAsync_ShouldReturnTypedCustomer()
    {
        // Arrange
        var id = Guid.NewGuid();
        var customer = new IndividualCustomer { Id = id, Email = "individual@example.com", PhoneNumber = "+905551112233" };

        _individualCustomerRepositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        // Act
        var result = await _customerService.GetIndividualCustomerByIdAsync(id);

        // Assert
        result.Should().BeSameAs(customer);
    }

    [Fact]
    public async Task GetCorporateCustomerByIdAsync_ShouldReturnTypedCustomer()
    {
        // Arrange
        var id = Guid.NewGuid();
        var customer = new CorporateCustomer { Id = id, Email = "corp@example.com", PhoneNumber = "+905559998877" };

        _corporateCustomerRepositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        // Act
        var result = await _customerService.GetCorporateCustomerByIdAsync(id);

        // Assert
        result.Should().BeSameAs(customer);
    }

    [Fact]
    public async Task GetIndividualCustomersAsync_ShouldReturnTypedList()
    {
        // Arrange
        var customers = new List<IndividualCustomer>
        {
            new() { Id = Guid.NewGuid(), Email = "one@example.com", PhoneNumber = "+905551112233" },
            new() { Id = Guid.NewGuid(), Email = "two@example.com", PhoneNumber = "+905551112234" }
        };

        _individualCustomerRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(customers);

        // Act
        var result = await _customerService.GetIndividualCustomersAsync();

        // Assert
        result.Should().BeEquivalentTo(customers);
    }

    [Fact]
    public async Task GetCorporateCustomersAsync_ShouldReturnTypedList()
    {
        // Arrange
        var customers = new List<CorporateCustomer>
        {
            new() { Id = Guid.NewGuid(), Email = "one@example.com", PhoneNumber = "+905559998877" },
            new() { Id = Guid.NewGuid(), Email = "two@example.com", PhoneNumber = "+905559998878" }
        };

        _corporateCustomerRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(customers);

        // Act
        var result = await _customerService.GetCorporateCustomersAsync();

        // Assert
        result.Should().BeEquivalentTo(customers);
    }
}
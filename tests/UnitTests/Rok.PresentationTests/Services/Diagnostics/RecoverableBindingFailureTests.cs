using System.Runtime.InteropServices;
using Rok.Services.Diagnostics;

namespace Rok.PresentationTests.Services.Diagnostics;

public class RecoverableBindingFailureTests
{
    private const string BindingFrames = "EH.DispatchEx < CastExtensions.As < ArtistsPage_obj4_Bindings.ProcessBindings < IDataTemplateComponent.Do_Abi_ProcessBindings_1";

    [Fact(DisplayName = "null_reference_from_generated_bindings_is_recoverable")]
    public void IsMatch_NullReferenceFromGeneratedBindings_ReturnsTrue()
    {
        // Arrange
        FirstChanceSite site = new(90, BindingFrames);

        // Act
        bool result = RecoverableBindingFailure.IsMatch(new NullReferenceException(), site);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "argument_null_from_generated_bindings_is_recoverable")]
    public void IsMatch_ArgumentNullFromGeneratedBindings_ReturnsTrue()
    {
        // Arrange
        FirstChanceSite site = new(90, BindingFrames);

        // Act
        bool result = RecoverableBindingFailure.IsMatch(new ArgumentNullException(), site);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "null_reference_without_first_chance_site_is_not_recoverable")]
    public void IsMatch_NoFirstChanceSite_ReturnsFalse()
    {
        // Act
        bool result = RecoverableBindingFailure.IsMatch(new NullReferenceException(), null);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "null_reference_thrown_outside_generated_bindings_is_not_recoverable")]
    public void IsMatch_ThrowSiteOutsideGeneratedBindings_ReturnsFalse()
    {
        // Arrange
        FirstChanceSite site = new(90, "EH.DispatchEx < PlayerService.Next < PlayerViewModel.OnNext");

        // Act
        bool result = RecoverableBindingFailure.IsMatch(new NullReferenceException(), site);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "stale_first_chance_site_is_not_recoverable")]
    public void IsMatch_StaleFirstChanceSite_ReturnsFalse()
    {
        // Arrange
        FirstChanceSite site = new(RecoverableBindingFailure.MaxAgeMs + 1, BindingFrames);

        // Act
        bool result = RecoverableBindingFailure.IsMatch(new NullReferenceException(), site);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "other_hresult_is_not_recoverable_even_from_generated_bindings")]
    public void IsMatch_OtherHResult_ReturnsFalse()
    {
        // Arrange
        FirstChanceSite site = new(90, BindingFrames);
        COMException layoutFailure = new("layout", unchecked((int)0x8000FFFF));

        // Act
        bool result = RecoverableBindingFailure.IsMatch(layoutFailure, site);

        // Assert
        Assert.False(result);
    }
}
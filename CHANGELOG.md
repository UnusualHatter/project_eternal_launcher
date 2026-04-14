# Changelog

All notable changes to Project Eternal: TF2 Launcher will be documented in this file.

## [v2.0.0] - 2026-04-14

### ADDED
- **Complete Mod Browser Integration** with GameBanana API v11
  - Real-time fetching of TF2 mods from GameBanana
  - Dynamic category system (Skins, Maps, UI, Sound, etc.)
  - Advanced sorting options (new, updated, obsolete, at_selection)
  - One-click download and installation with automatic ZIP extraction support
  - Support for .zip archives
  - Visual loading indicators and progress feedback

- **New Models and Services**
  - `GameSection` model for API category responses
  - Enhanced `GameBananaModService` with API v11 support
  - Robust JSON parsing with proper error handling
  - Local cache system for offline browsing

- **Enhanced UI Components**
  - Category dropdown populated from GameBanana sections
  - Sort dropdown with multiple ordering criteria
  - Refresh button with loading states
  - Improved card layout with better spacing and alignment
  - Progress bar for loading operations
  - Enhanced button styling and positioning

- **Internationalization Updates**
  - Changed "Baixar" to "Download" for English localization
  - Updated all Portuguese UI text to English
  - Consistent language throughout the interface

### CHANGED
- **API Migration**: Upgraded from RSS feeds to GameBanana API v11
  - More reliable data fetching
  - Better structured responses
  - Real-time category and filter support
  - Improved error handling

- **UI/UX Improvements**
  - Redesigned mod cards with better proportions (250x350px)
  - Enhanced thumbnail display with improved opacity
  - Better category badge styling
  - Optimized button layouts and spacing
  - Improved responsive design

- **Performance Enhancements**
  - Efficient JSON parsing with proper async handling
  - Optimized HTTP client configuration
  - Better memory management for large mod catalogs
  - Improved caching strategies

### FIXED
- **Download Button Issues**
  - Fixed compilation errors in download functionality
  - Resolved button text localization problems
  - Corrected button positioning and visibility states

- **JSON Parsing Errors**
  - Fixed null-conditional operator issues with JsonElement
  - Implemented proper TryGetProperty patterns
  - Added robust error handling for malformed API responses

- **Network Error Handling**
  - Comprehensive try-catch blocks for all API calls
  - Graceful fallback to cached data on network failures
  - User-friendly error messages in status bar
  - Proper logging of all network-related errors

- **Build Issues**
  - Resolved all compilation errors
  - Fixed missing converter references
  - Corrected namespace imports
  - Ensured successful build verification

### IMPROVED
- **Error Resilience**
  - Network failures no longer crash the application
  - Automatic retry mechanisms for failed requests
  - Fallback to local cache when API is unavailable
  - Clear user feedback during error conditions

- **Code Architecture**
  - Better separation of concerns between UI and business logic
  - Improved async/await patterns
  - Enhanced logging throughout the application
  - More maintainable and extensible code structure

- **User Experience**
  - Real-time catalog updates when filters change
  - Loading states prevent user confusion
  - Intuitive category and sort controls
  - Consistent visual feedback for all interactions

### TECHNICAL DETAILS

#### API Integration
- **Endpoints Used**:
  - `https://gamebanana.com/apiv11/Game/297/Sections` - Category fetching
  - `https://gamebanana.com/apiv11/Game/297/Subfeed` - General mod listing
  - `https://gamebanana.com/apiv11/Section/{ID}/Subfeed` - Category-specific mods
  - `https://api.gamebanana.com/Core/Item/Data` - Download URL resolution

#### File Structure Changes
```
src/LauncherTF2/
Models/
  - GameSection.cs (NEW)
Services/
  - GameBananaModService.cs (MAJOR REFACTOR)
ViewModels/
  - ModsViewModel.cs (ENHANCED)
Views/
  - ModsView.xaml (UI IMPROVEMENTS)
```

#### Dependencies Updated
- Added `System.Text.Json` for API response parsing
- Enhanced `System.Net.Http` usage with proper headers
- Improved `System.IO.Compression` for archive extraction

### BREAKING CHANGES
- **GameBananaModService.GetCatalogAsync()** signature updated to support section and sort parameters
- **ModsViewModel** new properties for sections and sorting (backward compatible)
- UI now requires English localization (Portuguese text removed)

### DEPRECATED
- Old RSS-based mod fetching system (replaced by API v11)
- Static category definitions (now dynamic from API)

### SECURITY
- Enhanced HTTP client with proper user agent
- Input validation for all API parameters
- Safe file extraction with path validation
- Proper handling of downloaded content

---

## [v1.0.0] - 2026-04-14

### ADDED
- Initial TF2 Launcher framework
- Basic WPF MVVM architecture
- Settings management system
- Autoexec configuration generation
- Steam integration for game launching
- Discord Rich Presence integration
- Local mod management
- System tray functionality
- Basic RPC monitoring

### FIXED
- Tray icon compilation errors
- Logger method signature issues
- Async method warnings
- Basic build configuration

---

## Version History Summary

- **v2.0.0**: Major feature release with complete GameBanana integration
- **v1.0.0**: Initial MVP release with core launcher functionality

## Upcoming Features (Roadmap)

### v2.1.0 (Planned)
- Mod dependency management
- Conflict detection system
- User ratings and reviews
- Advanced search with filters
- Batch download operations

### v2.2.0 (Planned)
- Cloud sync for settings and mods
- Mod profiles and configurations
- Analytics and usage statistics
- Performance optimizations
- Enhanced error reporting

### v3.0.0 (Future)
- Complete UI redesign
- Plugin system support
- Multi-game launcher expansion
- Advanced scripting support
- Community features integration

---

## Development Notes

### Build Instructions
```bash
# Build the project
dotnet build src/LauncherTF2/LauncherTF2.csproj

# Run the application
dotnet run --project src/LauncherTF2/LauncherTF2.csproj
```

### API Rate Limits
- GameBanana API has rate limiting considerations
- Local cache helps reduce API calls
- Implement exponential backoff for failed requests

### Performance Considerations
- Large mod catalogs may require pagination
- Image loading should be lazy-loaded
- Consider virtualization for long lists
- Monitor memory usage during extended sessions

### Known Issues
- Some GameBanana mods may have incomplete metadata
- Network timeouts can occur on slow connections
- Archive extraction may fail for password-protected files
- Very large mods may require additional timeout handling

---

*This changelog follows the [Keep a Changelog](https://keepachangelog.com/) format and was last updated on 2026-04-14.*

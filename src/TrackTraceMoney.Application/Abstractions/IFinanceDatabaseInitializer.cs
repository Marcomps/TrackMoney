namespace TrackTraceMoney.Application.Abstractions;

/// <summary>
/// Ensures the currently-active local profile's on-device finance database (schema migrations plus
/// the README §12 default-category seed data) is ready for use. Idempotent — safe to call every time
/// a profile becomes active, not just the first time (mirrors the underlying category seeder's own
/// already-idempotent guard).
///
/// Introduced alongside the local, password-less multi-profile feature so its two call sites —
/// <c>MauiProgram.cs</c>'s startup bootstrap (existing profile, or one just created by the
/// existing-install migration) and <c>CreateFirstProfileViewModel</c>'s first-profile-creation flow
/// (a genuinely brand-new install) — share one implementation instead of duplicating the
/// migrate-then-seed sequence. It also keeps App-layer ViewModels from needing a direct reference to
/// Infrastructure-only types (the finance <c>DbContext</c>, the category seeder) just to satisfy this
/// one bootstrap need — every other ViewModel in this codebase depends only on
/// <see cref="TrackTraceMoney.Application.Abstractions"/> types, and this keeps that true even for the
/// one ViewModel with a genuine bootstrap responsibility.
/// </summary>
public interface IFinanceDatabaseInitializer
{
    Task EnsureReadyAsync(CancellationToken ct = default);
}

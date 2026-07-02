<?php
/**
 * The full one-page landing content. Shared by front-page.php and index.php.
 *
 * @package NanoVault
 */

$nv_download = esc_url( nanovault_opt( 'download_url' ) );
$nv_coffee   = esc_url( nanovault_opt( 'coffee_url' ) );
$nv_github   = esc_url( nanovault_opt( 'github_url' ) );
$nv_version  = esc_html( nanovault_opt( 'app_version' ) );
$nv_preview  = get_theme_file_uri( 'assets/img/app-preview.png' );
$nv_ads      = nanovault_opt( 'adsense_code' );
?>

<!-- HERO -->
<section class="hero">
	<div class="hero__bg" aria-hidden="true"></div>
	<div class="wrap hero__grid">
		<div>
			<span class="eyebrow" data-reveal>
				<svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor"><path d="M12 2l2.4 7.4H22l-6 4.6 2.3 7.4-6.3-4.6L5.7 21 8 14 2 9.4h7.6z"/></svg>
				<?php esc_html_e( '100% free · no paywall · never touches your iPod', 'nanovault' ); ?>
			</span>
			<h1 data-reveal data-delay="1"><?php esc_html_e( 'Rescue the music off your', 'nanovault' ); ?> <span class="grad"><?php esc_html_e( 'old iPod.', 'nanovault' ); ?></span></h1>
			<p class="hero__lead" data-reveal data-delay="2">
				<?php echo wp_kses_post( __( 'NanoVault copies every song from your iPod nano into a tidy folder on your PC — with real track names, artwork and playlists. No iTunes, no tech skills, and it opens your iPod <strong>read-only</strong> so nothing on it ever changes.', 'nanovault' ) ); ?>
			</p>
			<div class="hero__cta" data-reveal data-delay="3">
				<a class="btn btn--lg btn-col" href="<?php echo $nv_download; ?>">
					<span>&#10515;&nbsp; <?php esc_html_e( 'Download for Windows', 'nanovault' ); ?></span>
					<span class="sub"><?php printf( esc_html__( 'Free · Windows 10 & 11 · 64-bit · v%s', 'nanovault' ), $nv_version ); ?></span>
				</a>
				<a class="btn btn--ghost btn--lg" href="#how"><?php esc_html_e( 'See how it works', 'nanovault' ); ?></a>
			</div>
			<div class="hero__meta" data-reveal data-delay="4">
				<span><svg viewBox="0 0 24 24" fill="currentColor"><path d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/></svg> <?php esc_html_e( 'Works offline', 'nanovault' ); ?></span>
				<span><svg viewBox="0 0 24 24" fill="currentColor"><path d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/></svg> <?php esc_html_e( 'No ads in the app', 'nanovault' ); ?></span>
				<span><svg viewBox="0 0 24 24" fill="currentColor"><path d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/></svg> <?php esc_html_e( 'Nothing uploaded, ever', 'nanovault' ); ?></span>
			</div>
		</div>
		<div class="hero__art" data-reveal data-delay="2">
			<div class="device">
				<img src="<?php echo esc_url( $nv_preview ); ?>" alt="<?php esc_attr_e( 'The NanoVault app showing a detected iPod and a Back Up Music button', 'nanovault' ); ?>" width="980" height="700">
			</div>
			<div class="hero__chip hero__chip--tl"><span class="dot"></span> <?php esc_html_e( 'iPod found — 1,842 tracks', 'nanovault' ); ?></div>
			<div class="hero__chip hero__chip--br">
				<svg viewBox="0 0 24 24" fill="none" stroke="#1f9d55" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg>
				<?php esc_html_e( 'Every copy verified', 'nanovault' ); ?>
			</div>
		</div>
	</div>
</section>

<!-- TICKER -->
<div class="ticker">
	<div class="wrap ticker__inner">
		<span><svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 1 3 5v6c0 5.5 3.8 10.7 9 12 5.2-1.3 9-6.5 9-12V5z"/></svg> <?php esc_html_e( 'Read-only & safe', 'nanovault' ); ?></span>
		<span><svg viewBox="0 0 24 24" fill="currentColor"><path d="M5 20h14v-2H5zM19 9h-4V3H9v6H5l7 7z"/></svg> <?php esc_html_e( 'Copies exact audio', 'nanovault' ); ?></span>
		<span><svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 3a9 9 0 1 0 9 9h-2a7 7 0 1 1-7-7z"/><path d="M11 7h2v6h-2z"/></svg> <?php esc_html_e( 'Recovers real names', 'nanovault' ); ?></span>
		<span><svg viewBox="0 0 24 24" fill="currentColor"><path d="M4 4h16v4H4zm0 6h16v4H4zm0 6h10v4H4z"/></svg> <?php esc_html_e( 'Playlists & report', 'nanovault' ); ?></span>
		<span><svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2 2 7l10 5 10-5zM2 17l10 5 10-5M2 12l10 5 10-5"/></svg> <?php esc_html_e( 'No install fee, ever', 'nanovault' ); ?></span>
	</div>
</div>

<!-- CALLOUT -->
<section class="section">
	<div class="wrap">
		<div class="callout" data-reveal>
			<h2><?php esc_html_e( 'Fed up of “here’s 50 songs — now pay to unlock the rest”?', 'nanovault' ); ?></h2>
			<p><?php echo wp_kses_post( __( 'So were we. Plenty of iPod-transfer apps let you copy a handful of tracks, then hold the rest of <em>your own music</em> hostage behind a <span class="strike">£19.99</span> licence. NanoVault copies <strong>all of it</strong>, the first time, for nothing. If it saves your library and you fancy buying us a coffee, that’s the whole business model.', 'nanovault' ) ); ?></p>
		</div>
	</div>
</section>

<!-- FEATURES -->
<section class="section" id="features" style="padding-top:0">
	<div class="wrap">
		<div class="section-head" data-reveal>
			<span class="eyebrow"><?php esc_html_e( 'Everything you need', 'nanovault' ); ?></span>
			<h2><?php esc_html_e( 'A proper backup, not a teaser', 'nanovault' ); ?></h2>
			<p><?php esc_html_e( 'One click turns a drawer-bound iPod into an organised, future-proof music folder.', 'nanovault' ); ?></p>
		</div>
		<div class="grid-features">
			<?php
			$nv_features = array(
				array( '<rect x="7" y="2" width="10" height="20" rx="2"/><circle cx="12" cy="17" r="2.4"/>', __( 'Finds your iPod automatically', 'nanovault' ), __( 'Plug in and NanoVault detects it — no drive letters, no hidden folders, no renaming random files by hand.', 'nanovault' ) ),
				array( '<path d="M9 18V5l12-2v13"/><circle cx="6" cy="18" r="3"/><circle cx="18" cy="16" r="3"/>', __( 'Real names &amp; artwork', 'nanovault' ), __( 'Recovers titles, artists, albums and track numbers from the songs and the iPod’s own database — so a random filename becomes The Beatles / Revolver / 06 – Yellow Submarine.', 'nanovault' ) ),
				array( '<path d="M3 7h5l2-2h4l2 2h5v12H3z"/><circle cx="12" cy="13" r="3"/>', __( 'Tidy folders, your way', 'nanovault' ), __( 'Choose Artist / Album / Track and more. Windows-illegal characters, reserved names and mega-long paths are all handled for you.', 'nanovault' ) ),
				array( '<path d="M12 2 4 6v6c0 5 3.5 8.5 8 10 4.5-1.5 8-5 8-10V6z"/><path d="m9 12 2 2 4-4"/>', __( 'Every copy verified', 'nanovault' ), __( 'Each file is checked with a SHA-256 hash after copying, so you know the backup is bit-for-bit identical to the original.', 'nanovault' ) ),
				array( '<path d="M8 4h13M8 12h13M8 20h13M3 4h.01M3 12h.01M3 20h.01"/>', __( 'Playlists &amp; a report', 'nanovault' ), __( 'Get an .m3u8 of everything, your iPod’s own playlists rebuilt, and a tidy HTML report of exactly what was copied.', 'nanovault' ) ),
				array( '<path d="M21 12.8A9 9 0 1 1 11.2 3 7 7 0 0 0 21 12.8z"/>', __( 'Handles the messy stuff', 'nanovault' ), __( 'Skips exact duplicates, keeps both when names clash, copies protected songs as-is, and never once overwrites a file silently.', 'nanovault' ) ),
			);
			$nv_i = 0;
			foreach ( $nv_features as $f ) :
				$nv_i++;
				?>
				<article class="feature" data-reveal data-delay="<?php echo esc_attr( ( $nv_i - 1 ) % 3 + 1 ); ?>">
					<div class="feature__icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><?php echo $f[0]; // phpcs:ignore ?></svg></div>
					<h3><?php echo wp_kses_post( $f[1] ); ?></h3>
					<p><?php echo wp_kses_post( $f[2] ); ?></p>
				</article>
			<?php endforeach; ?>
		</div>
	</div>
</section>

<!-- HOW -->
<section class="section" id="how" style="background:var(--bg-elevated)">
	<div class="wrap">
		<div class="section-head" data-reveal>
			<span class="eyebrow"><?php esc_html_e( 'Four steps', 'nanovault' ); ?></span>
			<h2><?php esc_html_e( 'From drawer to done in minutes', 'nanovault' ); ?></h2>
		</div>
		<div class="steps">
			<?php
			$nv_steps = array(
				array( __( 'Plug in your iPod', 'nanovault' ), __( 'Connect it with its USB cable. NanoVault spots it on its own.', 'nanovault' ) ),
				array( __( 'Pick a folder', 'nanovault' ), __( 'Choose anywhere on your PC to save the music.', 'nanovault' ) ),
				array( __( 'Click Back Up Music', 'nanovault' ), __( 'Watch progress, pause or cancel any time. Your iPod is only read.', 'nanovault' ) ),
				array( __( 'Enjoy your library', 'nanovault' ), __( 'Organised files, playlists and a report — ready for anything.', 'nanovault' ) ),
			);
			$nv_s = 0;
			foreach ( $nv_steps as $step ) :
				$nv_s++;
				?>
				<div class="step" data-reveal data-delay="<?php echo esc_attr( $nv_s ); ?>">
					<div class="step__num"><?php echo esc_html( $nv_s ); ?></div>
					<h3><?php echo esc_html( $step[0] ); ?></h3>
					<p><?php echo esc_html( $step[1] ); ?></p>
				</div>
			<?php endforeach; ?>
		</div>
	</div>
</section>

<!-- STATS -->
<section class="section">
	<div class="wrap">
		<div class="stats">
			<div class="stat" data-reveal data-delay="1"><div class="stat__num"><span data-count="100" data-suffix="%">0%</span></div><div class="stat__label"><?php esc_html_e( 'Free — every feature', 'nanovault' ); ?></div></div>
			<div class="stat" data-reveal data-delay="2"><div class="stat__num"><span data-count="0">0</span></div><div class="stat__label"><?php esc_html_e( 'Songs held hostage', 'nanovault' ); ?></div></div>
			<div class="stat" data-reveal data-delay="3"><div class="stat__num">SHA-256</div><div class="stat__label"><?php esc_html_e( 'Verified copies', 'nanovault' ); ?></div></div>
			<div class="stat" data-reveal data-delay="4"><div class="stat__num"><span data-count="0">0</span></div><div class="stat__label"><?php esc_html_e( 'Bytes uploaded anywhere', 'nanovault' ); ?></div></div>
		</div>
	</div>
</section>

<!-- DEVICES -->
<section class="section" id="devices" style="background:var(--bg-elevated)">
	<div class="wrap">
		<div class="section-head" data-reveal>
			<span class="eyebrow"><?php esc_html_e( 'Compatibility', 'nanovault' ); ?></span>
			<h2><?php esc_html_e( 'Which iPods work?', 'nanovault' ); ?></h2>
			<p><?php esc_html_e( 'If Windows shows your iPod as a drive, NanoVault can read it. That covers the classic disk-mode iPods.', 'nanovault' ); ?></p>
		</div>
		<div class="devices">
			<div class="devcard" data-reveal data-delay="1">
				<h3><span class="pill pill--yes"><?php esc_html_e( 'Works', 'nanovault' ); ?></span></h3>
				<ul>
					<li><?php esc_html_e( 'iPod nano (4th gen) — the main target', 'nanovault' ); ?></li>
					<li><?php esc_html_e( 'iPod nano 1st–5th gen', 'nanovault' ); ?></li>
					<li><?php esc_html_e( 'iPod classic / video / photo', 'nanovault' ); ?></li>
					<li><?php esc_html_e( 'iPod mini', 'nanovault' ); ?></li>
				</ul>
			</div>
			<div class="devcard" data-reveal data-delay="2">
				<h3><span class="pill pill--maybe"><?php esc_html_e( 'Best effort', 'nanovault' ); ?></span></h3>
				<ul>
					<li><?php esc_html_e( 'iPod shuffle — songs copy fine; some names may fall back to the filename', 'nanovault' ); ?></li>
					<li><?php esc_html_e( 'Older / unusual databases — always falls back to reading the songs directly', 'nanovault' ); ?></li>
					<li><?php esc_html_e( 'Any iPod with “Enable disk use” switched on', 'nanovault' ); ?></li>
				</ul>
			</div>
			<div class="devcard" data-reveal data-delay="3">
				<h3><span class="pill pill--no"><?php esc_html_e( 'Not supported', 'nanovault' ); ?></span></h3>
				<ul>
					<li><?php esc_html_e( 'iPod touch & iPhone — they don’t mount as a drive', 'nanovault' ); ?></li>
					<li><?php esc_html_e( 'iPod nano 6th / 7th gen — no disk mode', 'nanovault' ); ?></li>
				</ul>
			</div>
		</div>
	</div>
</section>

<!-- SAFETY -->
<section class="section">
	<div class="wrap">
		<div class="safety" data-reveal>
			<div>
				<span class="eyebrow"><?php esc_html_e( 'Your iPod is safe', 'nanovault' ); ?></span>
				<h2><?php esc_html_e( 'Read-only, by design', 'nanovault' ); ?></h2>
				<p><?php echo wp_kses_post( __( 'NanoVault is built so it <em>cannot</em> write to your iPod — that’s enforced in the code, not just a promise. The worst it can ever do is read.', 'nanovault' ) ); ?></p>
				<div class="safety__list">
					<?php
					$nv_safety = array(
						__( '<b>Never</b> deletes, renames, syncs, restores or formats anything on the iPod.', 'nanovault' ),
						__( 'Unplug mid-backup and finished songs are kept — just plug back in and it resumes.', 'nanovault' ),
						__( 'Completely offline. No account, no analytics, no artwork lookups, nothing leaves your PC.', 'nanovault' ),
						__( 'Open source under the MIT licence — read every line yourself.', 'nanovault' ),
					);
					foreach ( $nv_safety as $item ) :
						?>
						<div class="safety__item"><svg viewBox="0 0 24 24" fill="currentColor"><path d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/></svg><span><?php echo wp_kses_post( $item ); ?></span></div>
					<?php endforeach; ?>
				</div>
			</div>
			<div class="safety__badge" aria-hidden="true">
				<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2 4 6v6c0 5 3.5 8.5 8 10 4.5-1.5 8-5 8-10V6z"/><path d="m9 12 2 2 4-4"/></svg>
			</div>
		</div>
	</div>
</section>

<?php if ( trim( $nv_ads ) !== '' ) : ?>
<!-- OPTIONAL AD SLOT (from Customizer) -->
<div class="ad-slot"><div class="ad-slot__inner"><?php echo $nv_ads; // phpcs:ignore WordPress.Security.EscapeOutput ?></div></div>
<?php endif; ?>

<!-- DONATE -->
<section class="section" id="donate">
	<div class="wrap">
		<div class="donate" data-reveal>
			<span class="coffee-emoji" aria-hidden="true">&#9749;</span>
			<h2><?php esc_html_e( 'Like it? Buy me a coffee.', 'nanovault' ); ?></h2>
			<p><?php esc_html_e( 'NanoVault is free and always will be. There’s no upsell, no “pro” version, no locked features. If it rescued a library you thought was gone, a small tip keeps it maintained — entirely optional, no pressure.', 'nanovault' ); ?></p>
			<div class="donate__row">
				<a class="btn btn--coffee btn--lg" href="<?php echo $nv_coffee; ?>" target="_blank" rel="noopener">&#9749; <?php esc_html_e( 'Buy me a coffee', 'nanovault' ); ?></a>
				<a class="btn btn--ghost btn--lg" href="<?php echo $nv_github; ?>" target="_blank" rel="noopener">&#9733; <?php esc_html_e( 'Star it on GitHub', 'nanovault' ); ?></a>
			</div>
		</div>
	</div>
</section>

<!-- FAQ -->
<section class="section" id="faq" style="background:var(--bg-elevated)">
	<div class="wrap">
		<div class="section-head" data-reveal>
			<span class="eyebrow"><?php esc_html_e( 'Good to know', 'nanovault' ); ?></span>
			<h2><?php esc_html_e( 'Frequently asked questions', 'nanovault' ); ?></h2>
		</div>
		<div class="faq">
			<?php
			$nv_faq = array(
				array( __( 'Is it really free? What’s the catch?', 'nanovault' ), __( 'Yes — every feature, no trial, no track limit, no “pro” tier. It’s open source under the MIT licence. If you want to support it, there’s a coffee button; that’s the only ask.', 'nanovault' ) ),
				array( __( 'Will it change or wipe my iPod?', 'nanovault' ), __( 'No. NanoVault opens your iPod strictly read-only — it’s designed so it literally can’t write to the device. It only ever copies music off it.', 'nanovault' ) ),
				array( __( 'Do I need iTunes?', 'nanovault' ), __( 'No. As long as Windows shows your iPod as a drive, NanoVault works on its own. On some older models you may need to switch on “Enable disk use” once — the app walks you through it.', 'nanovault' ) ),
				array( __( 'Windows warned me about the download. Is it safe?', 'nanovault' ), __( 'The installer isn’t code-signed yet, so Windows SmartScreen may show a “more info” prompt on first run — that’s normal for small free apps. The full source is on GitHub if you’d like to build it yourself instead.', 'nanovault' ) ),
				array( __( 'What about protected (purchased) songs?', 'nanovault' ), __( 'They’re copied exactly as they are and clearly labelled. NanoVault never removes DRM — playing protected files still needs the Apple account they were bought with.', 'nanovault' ) ),
				array( __( 'Which iPods are supported?', 'nanovault' ), __( 'Classic disk-mode iPods — nano 1st–5th gen (4th gen is the main target), iPod classic, video, photo and mini. iPod touch/iPhone and the nano 6th/7th gen don’t mount as drives, so they aren’t supported.', 'nanovault' ) ),
			);
			foreach ( $nv_faq as $qa ) :
				?>
				<details class="qa" data-reveal>
					<summary class="qa__q"><?php echo esc_html( $qa[0] ); ?>
						<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg>
					</summary>
					<div class="qa__a"><?php echo esc_html( $qa[1] ); ?></div>
				</details>
			<?php endforeach; ?>
		</div>
	</div>
</section>

<!-- FINAL CTA -->
<section class="section final">
	<div class="wrap">
		<div class="card" data-reveal>
			<h2><?php esc_html_e( 'Get your music back.', 'nanovault' ); ?></h2>
			<p><?php esc_html_e( 'Free, safe, and it’ll never ask you to pay to unlock the rest of your own songs.', 'nanovault' ); ?></p>
			<a class="btn btn--lg" href="<?php echo $nv_download; ?>">&#10515;&nbsp; <?php esc_html_e( 'Download NanoVault for Windows', 'nanovault' ); ?></a>
		</div>
	</div>
</section>

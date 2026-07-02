<?php
/**
 * Site footer.
 *
 * @package NanoVault
 */

$nv_download = esc_url( nanovault_opt( 'download_url' ) );
$nv_github   = esc_url( nanovault_opt( 'github_url' ) );
$nv_coffee   = esc_url( nanovault_opt( 'coffee_url' ) );
$nv_logo     = get_theme_file_uri( 'assets/img/logo.png' );
?>

<footer class="footer">
	<div class="wrap">
		<div class="footer__grid">
			<div class="footer__brand">
				<a class="brand" href="#top"><img src="<?php echo esc_url( $nv_logo ); ?>" alt=""><?php bloginfo( 'name' ); ?></a>
				<p><?php esc_html_e( 'A free, open-source tool to back up the music on your iPod nano — safely, and without ever changing the device.', 'nanovault' ); ?></p>
			</div>
			<div class="footer__cols">
				<div class="footer__col">
					<h4><?php esc_html_e( 'Product', 'nanovault' ); ?></h4>
					<a href="#features"><?php esc_html_e( 'Features', 'nanovault' ); ?></a>
					<a href="#how"><?php esc_html_e( 'How it works', 'nanovault' ); ?></a>
					<a href="#devices"><?php esc_html_e( 'Supported iPods', 'nanovault' ); ?></a>
					<a href="#faq"><?php esc_html_e( 'FAQ', 'nanovault' ); ?></a>
				</div>
				<div class="footer__col">
					<h4><?php esc_html_e( 'Get it', 'nanovault' ); ?></h4>
					<a href="<?php echo $nv_download; ?>"><?php esc_html_e( 'Download', 'nanovault' ); ?></a>
					<a href="<?php echo $nv_github; ?>"><?php esc_html_e( 'Source code', 'nanovault' ); ?></a>
					<a href="<?php echo esc_url( trailingslashit( nanovault_opt( 'github_url' ) ) . 'issues' ); ?>"><?php esc_html_e( 'Report a problem', 'nanovault' ); ?></a>
				</div>
				<div class="footer__col">
					<h4><?php esc_html_e( 'Support', 'nanovault' ); ?></h4>
					<a href="<?php echo $nv_coffee; ?>"><?php esc_html_e( 'Buy me a coffee', 'nanovault' ); ?></a>
					<a href="<?php echo $nv_github; ?>"><?php esc_html_e( 'Star on GitHub', 'nanovault' ); ?></a>
				</div>
			</div>
		</div>
		<p class="footer__disclaimer">
			<?php esc_html_e( 'NanoVault is for backing up music you own or are authorised to copy. It does not remove DRM or bypass any protection. Apple, iPod and iTunes are trademarks of Apple Inc.; NanoVault is an independent project and is not affiliated with or endorsed by Apple. The installer is currently unsigned and community-tested.', 'nanovault' ); ?>
		</p>
		<div class="footer__note">
			<span>&copy; <span data-year><?php echo esc_html( gmdate( 'Y' ) ); ?></span> <?php bloginfo( 'name' ); ?> &middot; <?php esc_html_e( 'MIT licensed', 'nanovault' ); ?></span>
			<span><?php esc_html_e( 'Made for people with old iPods and long memories.', 'nanovault' ); ?></span>
		</div>
	</div>
</footer>

<?php wp_footer(); ?>
</body>
</html>

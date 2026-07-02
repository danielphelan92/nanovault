<?php
/**
 * Site header and sticky navigation.
 *
 * @package NanoVault
 */

$nv_download = esc_url( nanovault_opt( 'download_url' ) );
$nv_logo     = has_custom_logo() ? '' : get_theme_file_uri( 'assets/img/logo.png' );
?>
<!DOCTYPE html>
<html <?php language_attributes(); ?>>
<head>
	<meta charset="<?php bloginfo( 'charset' ); ?>">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<link rel="preconnect" href="https://fonts.googleapis.com">
	<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
	<?php wp_head(); ?>
</head>
<body <?php body_class(); ?>>
<?php wp_body_open(); ?>

<header class="nav">
	<div class="wrap nav__inner">
		<a class="brand" href="<?php echo esc_url( home_url( '/' ) ); ?>">
			<?php if ( has_custom_logo() ) : ?>
				<?php the_custom_logo(); ?>
			<?php else : ?>
				<img src="<?php echo esc_url( $nv_logo ); ?>" alt="<?php bloginfo( 'name' ); ?> logo">
			<?php endif; ?>
			<?php bloginfo( 'name' ); ?>
		</a>
		<button class="nav__toggle" aria-label="<?php esc_attr_e( 'Menu', 'nanovault' ); ?>" aria-expanded="false">
			<svg viewBox="0 0 24 24" width="26" height="26" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 7h16M4 12h16M4 17h16"/></svg>
		</button>
		<nav class="nav__links">
			<a href="#features"><?php esc_html_e( 'Features', 'nanovault' ); ?></a>
			<a href="#how"><?php esc_html_e( 'How it works', 'nanovault' ); ?></a>
			<a href="#devices"><?php esc_html_e( 'Supported iPods', 'nanovault' ); ?></a>
			<a href="#faq"><?php esc_html_e( 'FAQ', 'nanovault' ); ?></a>
			<a href="#donate">&hearts; <?php esc_html_e( 'Donate', 'nanovault' ); ?></a>
			<a class="btn nav__cta" href="<?php echo $nv_download; ?>"><?php esc_html_e( 'Download free', 'nanovault' ); ?></a>
		</nav>
	</div>
</header>

<span id="top"></span>

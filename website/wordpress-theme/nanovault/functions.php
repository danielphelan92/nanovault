<?php
/**
 * NanoVault theme setup, asset loading, and configuration helpers.
 *
 * @package NanoVault
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit; // No direct access.
}

define( 'NANOVAULT_VERSION', '1.0.0' );

/**
 * Theme configuration with sensible defaults. Every value is editable under
 * Appearance → Customize → NanoVault, so no PHP editing is required.
 */
function nanovault_defaults() {
	return array(
		'download_url' => 'https://github.com/danielphelan92/nanovault/releases/download/v1.0.0/NanoVault-Setup-1.0.0.exe',
		'coffee_url'   => '#', // Set your Buy Me a Coffee / Ko-fi / PayPal link in Customizer.
		'github_url'   => 'https://github.com/danielphelan92/nanovault',
		'app_version'  => '1.0.0',
		'adsense_code' => '',
	);
}

/**
 * Read a configured option, falling back to the default.
 *
 * @param string $key Option key from nanovault_defaults().
 * @return string
 */
function nanovault_opt( $key ) {
	$defaults = nanovault_defaults();
	$default  = isset( $defaults[ $key ] ) ? $defaults[ $key ] : '';
	return get_theme_mod( 'nanovault_' . $key, $default );
}

/**
 * Basic theme supports.
 */
function nanovault_setup() {
	add_theme_support( 'title-tag' );
	add_theme_support( 'post-thumbnails' );
	add_theme_support( 'html5', array( 'style', 'script' ) );
	add_theme_support(
		'custom-logo',
		array(
			'height'      => 68,
			'width'       => 68,
			'flex-height' => true,
			'flex-width'  => true,
		)
	);
	register_nav_menus(
		array(
			'primary' => __( 'Primary Menu', 'nanovault' ),
		)
	);
}
add_action( 'after_setup_theme', 'nanovault_setup' );

/**
 * Enqueue fonts, styles and scripts.
 */
function nanovault_assets() {
	wp_enqueue_style(
		'nanovault-fonts',
		'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800;850&display=swap',
		array(),
		null
	);

	wp_enqueue_style(
		'nanovault-main',
		get_theme_file_uri( 'assets/css/main.css' ),
		array(),
		NANOVAULT_VERSION
	);

	// WordPress requires the root style.css to be enqueued too (theme header).
	wp_enqueue_style(
		'nanovault-style',
		get_stylesheet_uri(),
		array( 'nanovault-main' ),
		NANOVAULT_VERSION
	);

	wp_enqueue_script(
		'nanovault-main',
		get_theme_file_uri( 'assets/js/main.js' ),
		array(),
		NANOVAULT_VERSION,
		true
	);
}
add_action( 'wp_enqueue_scripts', 'nanovault_assets' );

require get_theme_file_path( 'inc/customizer.php' );

<?php
/**
 * Customizer settings for NanoVault: all the links a site owner needs to fill
 * in, grouped under a single "NanoVault" panel.
 *
 * @package NanoVault
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/**
 * Register the NanoVault customizer section and controls.
 *
 * @param WP_Customize_Manager $wp_customize Customizer manager.
 */
function nanovault_customize_register( $wp_customize ) {
	$wp_customize->add_section(
		'nanovault_options',
		array(
			'title'       => __( 'NanoVault', 'nanovault' ),
			'priority'    => 30,
			'description' => __( 'Links and settings for the NanoVault landing page. The installer is ~49 MB — too big for a WordPress media upload — so host it on GitHub Releases (free) and paste the direct link below.', 'nanovault' ),
		)
	);

	$controls = array(
		'download_url' => array(
			'label'       => __( 'Download URL (.exe)', 'nanovault' ),
			'description' => __( 'Direct link to NanoVault-Setup-1.0.0.exe, e.g. a GitHub Releases asset URL.', 'nanovault' ),
			'type'        => 'url',
			'sanitize'    => 'esc_url_raw',
		),
		'coffee_url'   => array(
			'label'       => __( 'Buy Me a Coffee / donation URL', 'nanovault' ),
			'description' => __( 'e.g. https://www.buymeacoffee.com/yourname or a Ko-fi / PayPal link.', 'nanovault' ),
			'type'        => 'url',
			'sanitize'    => 'esc_url_raw',
		),
		'github_url'   => array(
			'label'       => __( 'GitHub repository URL', 'nanovault' ),
			'description' => __( 'Used for the source-code and "report a problem" links.', 'nanovault' ),
			'type'        => 'url',
			'sanitize'    => 'esc_url_raw',
		),
		'app_version'  => array(
			'label'       => __( 'App version label', 'nanovault' ),
			'description' => __( 'Shown on the download button, e.g. 1.0.0.', 'nanovault' ),
			'type'        => 'text',
			'sanitize'    => 'sanitize_text_field',
		),
	);

	$defaults = nanovault_defaults();

	foreach ( $controls as $key => $control ) {
		$wp_customize->add_setting(
			'nanovault_' . $key,
			array(
				'default'           => $defaults[ $key ],
				'sanitize_callback' => $control['sanitize'],
				'transport'         => 'refresh',
			)
		);
		$wp_customize->add_control(
			'nanovault_' . $key,
			array(
				'label'       => $control['label'],
				'description' => $control['description'],
				'section'     => 'nanovault_options',
				'type'        => $control['type'],
			)
		);
	}

	// Optional AdSense / ad HTML. Rendered raw, so only the site owner sets it.
	$wp_customize->add_setting(
		'nanovault_adsense_code',
		array(
			'default'           => $defaults['adsense_code'],
			'sanitize_callback' => 'nanovault_sanitize_ad_html',
			'transport'         => 'refresh',
		)
	);
	$wp_customize->add_control(
		'nanovault_adsense_code',
		array(
			'label'       => __( 'Ad code (optional)', 'nanovault' ),
			'description' => __( 'Paste a Google AdSense (or other) ad unit snippet to show a single, unobtrusive ad slot below the safety section. Leave blank for none.', 'nanovault' ),
			'section'     => 'nanovault_options',
			'type'        => 'textarea',
		)
	);
}
add_action( 'customize_register', 'nanovault_customize_register' );

/**
 * Allow the ad-code field to hold script/ins markup (site-owner only input).
 *
 * @param string $input Raw textarea value.
 * @return string
 */
function nanovault_sanitize_ad_html( $input ) {
	return $input; // Trusted admin-only field; kept verbatim for ad snippets.
}

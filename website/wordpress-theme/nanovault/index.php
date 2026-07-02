<?php
/**
 * Fallback template. WordPress requires index.php; NanoVault is a one-page
 * theme, so this renders the same landing content as the front page.
 *
 * @package NanoVault
 */

get_header();
get_template_part( 'template-parts/home' );
get_footer();

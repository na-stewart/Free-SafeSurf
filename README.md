<!-- Improved compatibility of back to top link: See: https://github.com/othneildrew/Best-README-Template/pull/73 -->
<!--
*** Thanks for checking out the Best-README-Template. If you have a suggestion
*** that would make this better, please fork the repo and create a pull request
*** or simply open an issue with the tag "enhancement".
*** Don't forget to give the project a star!
*** Thanks again! Now go create something AMAZING! :D
-->

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://github.com/othneildrew/Best-README-Template">
    <img src="https://github.com/na-stewart/SafeSurf/blob/master/SafeSurf.png" alt="Logo" width="350" height="350">
  </a>
  <p align="center">
    Blocks harmful content and prohibits circumvention.
    <br />
  </p>
</div>

<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li><a href="#about-the-project">About The Project</a></li>
    <li><a href="#configuration">Configuration</a></li>
    <li><a href="#troubleshooting">Troubleshooting</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
  </ol>
</details>

<!-- ABOUT THE PROJECT -->
## About The Project

SafeSurf for Windows can protect from harmful materials online.

Bypass prevention prohibits users from disabling or making changes to settings for a certain period of time.

Privacy friendly as absolutely no data is collected and is completely free and open source.

<!-- GETTING STARTED -->
## Configuration

SafeSurf has been designed to be simple to use, just download the most recent version from [here](https://github.com/na-stewart/SafeSurf/releases) and run `SafeSurf`. 

May require installation of ***.NET Desktop Runtime:*** [https://dotnet.microsoft.com/en-us/download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

This program will ***only*** be available for download on this repository. Any instances found elsewhere may be malicious.

<ins>Before activation, make sure you understand what each setting does.</ins>

### Hosts Filter
Operating system level content filtering is implemented via a hosts file which is used to map a connection between an IP address and domain name. SafeSurf routes A massive amount of questionable domains to `0.0.0.0` to reject the user's request to visit the site. The main drawback is that some content may break through if a domain is not yet registered with SafeSurf and may not be as accurate as CleanBrowsing. 

- Adult: Rejects all questionable adult content.
- Gambling: Rejects all online gambling sites and questionable adult content.
- Family: Rejects all questionable adult content as well as mixed content sites (like Reddit).

### CleanBrowsing DNS Filter
Network level content filtering is implemented via a service called [CleanBrowsing](https://cleanbrowsing.org/) which provides free, private DNS addresses that effectively reject questionable content. The main drawback is that setting a custom DNS on Windows may lead to connectivity issues depending on your internet service provider. Some users may be uncomfortable funneling network activity through CleanBrowsing, be sure to read their [privacy policy](https://cleanbrowsing.org/privacy) and investigate further.

- Adult: Rejects all questionable adult content.
- Family: Rejects all questionable adult content and blocks access to mixed content sites (like Reddit).
  
### Days Enforced
This setting will initiate the SafeSurf enforcer (for all local accounts) which prevents circumvention. It has been designed to be difficult for technical and non-technical users to get around. Once activated, SafeSurf settings cannot be overridden until it expires. It is recommended to find what configuration works well for you before enabling the enforcer.

## Troubleshooting

### I cannot connect to the internet.
In the instance that SafeSurf causes connectivity issues, it is recommended to utilize the Windows network adapter troubleshooter. This will in most cases fix your connectivity issues.

### Why does SafeSurf ask for administrator priviliges?
Functionality including custom DNS, manipulating the hosts file, and some anti-circumvention measures require elevation to function.

### Accountability Partner
If necessary, set up an "accountability partner" account that would act as the adminstrator of your device. Next remove administrator privileges from other users, 
including yourself if needed. The password for the account would then be disclosed to someone entrusted to provide the password/permissions only when required.

### SafeSurf is being detected as a virus.
For transparency, it's important to disclose that SafeSurf may potentially be detected as a virus due it's anti-circumvention measures and because it is not yet registered with a publisher. To reiterate, SafeSurf is not a malicious program. This can be solved by making exceptions for SafeSurf executables as they are detected by your respective antivirus.

### Help!
If you experience any problems with SafeSurf, [submit an issue](https://github.com/na-stewart/FreeSafeSurf/issues) or [contact me](https://blog.na-stewart.com/contact).

<!-- CONTRIBUTING -->
## Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".
Don't forget to give the project a star! Thanks again!

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

<!-- LICENSE -->
## License

Distributed under the MIT License. See `LICENSE.txt` for more information.

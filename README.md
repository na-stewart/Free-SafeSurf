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

Privacy friendly as no data is collected and is completely free and open source.

<!-- GETTING STARTED -->
## Configuration

Download the most recent version from [here](https://github.com/na-stewart/SafeSurf/releases) and run `SafeSurf`. 

May require installation of [.NET Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

This program will ***only*** be available for download on this repository and instances found elsewhere may be malicious.

<ins>Before activation, make sure you understand what each setting does.</ins>

### Hosts Filter
Operating system level content filtering is implemented via the hosts file which is configured to map questionable domains to an unreachable IP address. 

- Adult: Rejects all adult content.
- Gambling: Rejects all online gambling sites and adult content.
- Family: Rejects all adult content as well as mixed content sites (like Reddit).

### CleanBrowsing DNS Filter (recommended)
Network level content filtering is implemented via [CleanBrowsing](https://cleanbrowsing.org/) which provides a DNS service that effectively rejects questionable content.

- Adult: Rejects all adult content.
- Family: Rejects all adult content and blocks access to mixed content sites (like Reddit).
  
### Days Enforced
This setting will prohibit users from disabling or making changes to SafeSurf for a certain period of time. It is recommended to find what configuration works well for you before enabling it.

## Troubleshooting

### I cannot connect to the internet.
If SafeSurf causes connectivity issues, utilize the Windows network adapter troubleshooter.

### Why does SafeSurf ask for administrator priviliges?
Functionality including manipulating DNS and hosts as well as some anti-circumvention measures require elevation to function.

### Accountability Partner
If necessary, set up an "accountability partner" account that would act as the adminstrator of your device. Next remove administrator privileges from other users, 
including yourself if needed. The password for the account would then be disclosed to someone entrusted to provide the password/permissions only when required.

### SafeSurf is being detected as a virus.
SafeSurf may potentially be detected as a virus due it's anti-circumvention measures and because it is not yet registered with a publisher. To reiterate, this is not a malicious program. Able to be fixed by making exceptions for SafeSurf executables as they are detected with your respective antivirus.

### Help!
If you experience any other problems, [submit an issue](https://github.com/na-stewart/FreeSafeSurf/issues) or [contact me](https://blog.na-stewart.com/contact).

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
